using System.Globalization;
using Microsoft.Data.Sqlite;
using XArchiver.Core.Interfaces;
using XArchiver.Core.Models;
using XArchiver.Core.Utilities;

namespace XArchiver.Core.Services;

public sealed class ArchiveIndexRepository : IArchiveIndexRepository
{
    public async Task<ArchivedPostRecord?> GetPostAsync(ArchiveProfile profile, string postId, CancellationToken cancellationToken)
    {
        string databasePath = ArchivePathBuilder.GetDatabasePath(profile);
        if (!File.Exists(databasePath))
        {
            return null;
        }

        await using SqliteConnection connection = CreateConnection(databasePath);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using SqliteCommand postCommand = connection.CreateCommand();
        postCommand.CommandText = """
            SELECT PostId, ProfileId, UserId, Username, TextContent, CreatedAtUtc, PostType, ConversationId,
                   InReplyToUserId, LikeCount, ReplyCount, RepostCount, QuoteCount, TextRelativePath, MetadataRelativePath
            FROM Posts
            WHERE PostId = $postId
            LIMIT 1;
            """;
        postCommand.Parameters.AddWithValue("$postId", postId);

        await using SqliteDataReader reader = await postCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        ArchivedPostRecord post = ReadPost(reader);
        Dictionary<string, List<ArchivedMediaRecord>> mediaByPostId = await LoadMediaAsync(connection, cancellationToken).ConfigureAwait(false);
        if (mediaByPostId.TryGetValue(post.PostId, out List<ArchivedMediaRecord>? media))
        {
            post.Media = media;
        }

        return post;
    }

    public async Task<IReadOnlySet<string>> GetArchivedPostIdsAsync(
        ArchiveProfile profile,
        IReadOnlyCollection<string> postIds,
        CancellationToken cancellationToken)
    {
        if (postIds.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        string databasePath = ArchivePathBuilder.GetDatabasePath(profile);
        if (!File.Exists(databasePath))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        await using SqliteConnection connection = CreateConnection(databasePath);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        List<string> parameterNames = [];
        await using SqliteCommand command = connection.CreateCommand();

        int index = 0;
        foreach (string postId in postIds.Distinct(StringComparer.Ordinal))
        {
            string parameterName = $"$postId{index++}";
            parameterNames.Add(parameterName);
            command.Parameters.AddWithValue(parameterName, postId);
        }

        command.CommandText = $"SELECT PostId FROM Posts WHERE PostId IN ({string.Join(", ", parameterNames)});";

        HashSet<string> archivedPostIds = new(StringComparer.Ordinal);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            archivedPostIds.Add(reader.GetString(0));
        }

        return archivedPostIds;
    }

    public async Task InitializeAsync(ArchiveProfile profile, CancellationToken cancellationToken)
    {
        string databasePath = ArchivePathBuilder.GetDatabasePath(profile);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

        await using SqliteConnection connection = CreateConnection(databasePath);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        string sql = """
            CREATE TABLE IF NOT EXISTS Posts (
                PostId TEXT PRIMARY KEY,
                ProfileId TEXT NOT NULL,
                UserId TEXT NOT NULL,
                Username TEXT NOT NULL,
                TextContent TEXT NOT NULL,
                CreatedAtUtc TEXT NOT NULL,
                PostType INTEGER NOT NULL,
                ConversationId TEXT NULL,
                InReplyToUserId TEXT NULL,
                LikeCount INTEGER NOT NULL,
                ReplyCount INTEGER NOT NULL,
                RepostCount INTEGER NOT NULL,
                QuoteCount INTEGER NOT NULL,
                TextRelativePath TEXT NULL,
                MetadataRelativePath TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS Media (
                PostId TEXT NOT NULL,
                MediaKey TEXT NOT NULL,
                Kind INTEGER NOT NULL,
                SourceUrl TEXT NOT NULL,
                RelativePath TEXT NULL,
                IsPartial INTEGER NOT NULL,
                Width INTEGER NULL,
                Height INTEGER NULL,
                DurationMs INTEGER NULL,
                PRIMARY KEY (PostId, MediaKey)
            );
            """;

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpsertAsync(ArchiveProfile profile, ArchivedPostRecord post, CancellationToken cancellationToken)
    {
        string databasePath = ArchivePathBuilder.GetDatabasePath(profile);
        await InitializeAsync(profile, cancellationToken).ConfigureAwait(false);

        await using SqliteConnection connection = CreateConnection(databasePath);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteTransaction transaction = connection.BeginTransaction();

        await using (SqliteCommand deleteMediaCommand = connection.CreateCommand())
        {
            deleteMediaCommand.Transaction = transaction;
            deleteMediaCommand.CommandText = "DELETE FROM Media WHERE PostId = $postId;";
            deleteMediaCommand.Parameters.AddWithValue("$postId", post.PostId);
            await deleteMediaCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (SqliteCommand postCommand = connection.CreateCommand())
        {
            postCommand.Transaction = transaction;
            postCommand.CommandText = """
                INSERT INTO Posts (
                    PostId, ProfileId, UserId, Username, TextContent, CreatedAtUtc, PostType, ConversationId,
                    InReplyToUserId, LikeCount, ReplyCount, RepostCount, QuoteCount, TextRelativePath, MetadataRelativePath)
                VALUES (
                    $postId, $profileId, $userId, $username, $textContent, $createdAtUtc, $postType, $conversationId,
                    $inReplyToUserId, $likeCount, $replyCount, $repostCount, $quoteCount, $textRelativePath, $metadataRelativePath)
                ON CONFLICT(PostId) DO UPDATE SET
                    ProfileId = excluded.ProfileId,
                    UserId = excluded.UserId,
                    Username = excluded.Username,
                    TextContent = excluded.TextContent,
                    CreatedAtUtc = excluded.CreatedAtUtc,
                    PostType = excluded.PostType,
                    ConversationId = excluded.ConversationId,
                    InReplyToUserId = excluded.InReplyToUserId,
                    LikeCount = excluded.LikeCount,
                    ReplyCount = excluded.ReplyCount,
                    RepostCount = excluded.RepostCount,
                    QuoteCount = excluded.QuoteCount,
                    TextRelativePath = excluded.TextRelativePath,
                    MetadataRelativePath = excluded.MetadataRelativePath;
                """;
            AddPostParameters(postCommand, post);
            await postCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (ArchivedMediaRecord media in post.Media)
        {
            await using SqliteCommand mediaCommand = connection.CreateCommand();
            mediaCommand.Transaction = transaction;
            mediaCommand.CommandText = """
                INSERT INTO Media (
                    PostId, MediaKey, Kind, SourceUrl, RelativePath, IsPartial, Width, Height, DurationMs)
                VALUES (
                    $postId, $mediaKey, $kind, $sourceUrl, $relativePath, $isPartial, $width, $height, $durationMs);
                """;
            mediaCommand.Parameters.AddWithValue("$postId", media.PostId);
            mediaCommand.Parameters.AddWithValue("$mediaKey", media.MediaKey);
            mediaCommand.Parameters.AddWithValue("$kind", (int)media.Kind);
            mediaCommand.Parameters.AddWithValue("$sourceUrl", media.SourceUrl);
            mediaCommand.Parameters.AddWithValue("$relativePath", (object?)media.RelativePath ?? DBNull.Value);
            mediaCommand.Parameters.AddWithValue("$isPartial", media.IsPartial ? 1 : 0);
            mediaCommand.Parameters.AddWithValue("$width", (object?)media.Width ?? DBNull.Value);
            mediaCommand.Parameters.AddWithValue("$height", (object?)media.Height ?? DBNull.Value);
            mediaCommand.Parameters.AddWithValue("$durationMs", (object?)media.DurationMs ?? DBNull.Value);
            await mediaCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ArchivedPostRecord>> QueryAsync(
        ArchiveProfile profile,
        ArchiveViewerFilter filter,
        CancellationToken cancellationToken)
    {
        string databasePath = ArchivePathBuilder.GetDatabasePath(profile);
        if (!File.Exists(databasePath))
        {
            return [];
        }

        await using SqliteConnection connection = CreateConnection(databasePath);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        List<string> allowedTypes = BuildAllowedPostTypes(filter);

        if (allowedTypes.Count == 0)
        {
            return [];
        }

        List<string> contentClauses = [];
        if (filter.IncludeTextPosts)
        {
            contentClauses.Add("Posts.TextContent <> ''");
        }

        if (filter.IncludeImagePosts)
        {
            contentClauses.Add($"EXISTS (SELECT 1 FROM Media WHERE Media.PostId = Posts.PostId AND Media.Kind = {(int)ArchiveMediaKind.Image})");
        }

        if (filter.IncludeVideoPosts)
        {
            contentClauses.Add($"EXISTS (SELECT 1 FROM Media WHERE Media.PostId = Posts.PostId AND Media.Kind = {(int)ArchiveMediaKind.Video})");
        }

        if (contentClauses.Count == 0)
        {
            return [];
        }

        await using SqliteCommand postCommand = connection.CreateCommand();
        postCommand.CommandText = $"""
            SELECT PostId, ProfileId, UserId, Username, TextContent, CreatedAtUtc, PostType, ConversationId,
                   InReplyToUserId, LikeCount, ReplyCount, RepostCount, QuoteCount, TextRelativePath, MetadataRelativePath
            FROM Posts
            WHERE PostType IN ({string.Join(", ", allowedTypes)})
              AND ($searchText = '' OR TextContent LIKE '%' || $searchText || '%' OR Username LIKE '%' || $searchText || '%')
              AND ({string.Join(" OR ", contentClauses)})
            ORDER BY CreatedAtUtc DESC;
            """;
        postCommand.Parameters.AddWithValue("$searchText", filter.SearchText.Trim());

        List<ArchivedPostRecord> posts = [];
        await using SqliteDataReader reader = await postCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            posts.Add(ReadPost(reader));
        }

        Dictionary<string, List<ArchivedMediaRecord>> mediaByPostId = await LoadMediaAsync(connection, cancellationToken).ConfigureAwait(false);
        foreach (ArchivedPostRecord post in posts)
        {
            if (mediaByPostId.TryGetValue(post.PostId, out List<ArchivedMediaRecord>? media))
            {
                post.Media = media;
            }
        }

        return posts;
    }

    public async Task<IReadOnlyList<ArchivedGalleryMediaRecord>> QueryGalleryMediaAsync(
        ArchiveProfile profile,
        ArchiveViewerFilter filter,
        CancellationToken cancellationToken)
    {
        string databasePath = ArchivePathBuilder.GetDatabasePath(profile);
        if (!File.Exists(databasePath))
        {
            return [];
        }

        await using SqliteConnection connection = CreateConnection(databasePath);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        List<string> allowedTypes = BuildAllowedPostTypes(filter);
        if (allowedTypes.Count == 0)
        {
            return [];
        }

        List<string> allowedMediaKinds = BuildAllowedGalleryMediaKinds(filter);
        if (allowedMediaKinds.Count == 0)
        {
            return [];
        }

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT Posts.PostId, Posts.Username, Posts.CreatedAtUtc, Posts.PostType,
                   Media.PostId, Media.MediaKey, Media.Kind, Media.SourceUrl, Media.RelativePath, Media.IsPartial, Media.Width, Media.Height, Media.DurationMs
            FROM Posts
            INNER JOIN Media ON Media.PostId = Posts.PostId
            WHERE Posts.PostType IN ({string.Join(", ", allowedTypes)})
              AND Media.Kind IN ({string.Join(", ", allowedMediaKinds)})
              AND ($searchText = '' OR Posts.TextContent LIKE '%' || $searchText || '%' OR Posts.Username LIKE '%' || $searchText || '%')
            ORDER BY Posts.CreatedAtUtc DESC, Media.MediaKey ASC;
            """;
        command.Parameters.AddWithValue("$searchText", filter.SearchText.Trim());

        List<ArchivedGalleryMediaRecord> results = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(
                new ArchivedGalleryMediaRecord
                {
                    CreatedAtUtc = DateTimeOffset.Parse(reader.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind),
                    Media = ReadMedia(reader, 4),
                    ParentPostId = reader.GetString(0),
                    PostType = (ArchivePostType)reader.GetInt32(3),
                    Username = reader.GetString(1),
                });
        }

        return results;
    }

    private static void AddPostParameters(SqliteCommand command, ArchivedPostRecord post)
    {
        command.Parameters.AddWithValue("$postId", post.PostId);
        command.Parameters.AddWithValue("$profileId", post.ProfileId.ToString());
        command.Parameters.AddWithValue("$userId", post.UserId);
        command.Parameters.AddWithValue("$username", post.Username);
        command.Parameters.AddWithValue("$textContent", post.Text);
        command.Parameters.AddWithValue("$createdAtUtc", post.CreatedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$postType", (int)post.PostType);
        command.Parameters.AddWithValue("$conversationId", (object?)post.ConversationId ?? DBNull.Value);
        command.Parameters.AddWithValue("$inReplyToUserId", (object?)post.InReplyToUserId ?? DBNull.Value);
        command.Parameters.AddWithValue("$likeCount", post.LikeCount);
        command.Parameters.AddWithValue("$replyCount", post.ReplyCount);
        command.Parameters.AddWithValue("$repostCount", post.RepostCount);
        command.Parameters.AddWithValue("$quoteCount", post.QuoteCount);
        command.Parameters.AddWithValue("$textRelativePath", (object?)post.TextRelativePath ?? DBNull.Value);
        command.Parameters.AddWithValue("$metadataRelativePath", (object?)post.MetadataRelativePath ?? DBNull.Value);
    }

    private static SqliteConnection CreateConnection(string databasePath)
    {
        return new SqliteConnection($"Data Source={databasePath}");
    }

    private static List<string> BuildAllowedGalleryMediaKinds(ArchiveViewerFilter filter)
    {
        List<string> allowedMediaKinds = [];
        if (filter.IncludeImagePosts)
        {
            allowedMediaKinds.Add(((int)ArchiveMediaKind.Image).ToString(CultureInfo.InvariantCulture));
        }

        if (filter.IncludeVideoPosts)
        {
            allowedMediaKinds.Add(((int)ArchiveMediaKind.Video).ToString(CultureInfo.InvariantCulture));
        }

        return allowedMediaKinds;
    }

    private static List<string> BuildAllowedPostTypes(ArchiveViewerFilter filter)
    {
        List<string> allowedTypes = [];
        if (filter.IncludeOriginalPosts)
        {
            allowedTypes.Add(((int)ArchivePostType.Original).ToString(CultureInfo.InvariantCulture));
        }

        if (filter.IncludeReplies)
        {
            allowedTypes.Add(((int)ArchivePostType.Reply).ToString(CultureInfo.InvariantCulture));
        }

        if (filter.IncludeQuotes)
        {
            allowedTypes.Add(((int)ArchivePostType.Quote).ToString(CultureInfo.InvariantCulture));
        }

        if (filter.IncludeReposts)
        {
            allowedTypes.Add(((int)ArchivePostType.Repost).ToString(CultureInfo.InvariantCulture));
        }

        return allowedTypes;
    }

    private static ArchivedMediaRecord ReadMedia(SqliteDataReader reader, int ordinalOffset = 0)
    {
        return new ArchivedMediaRecord
        {
            DurationMs = reader.IsDBNull(8 + ordinalOffset) ? null : reader.GetInt64(8 + ordinalOffset),
            Height = reader.IsDBNull(7 + ordinalOffset) ? null : reader.GetInt32(7 + ordinalOffset),
            IsPartial = reader.GetInt32(5 + ordinalOffset) == 1,
            Kind = (ArchiveMediaKind)reader.GetInt32(2 + ordinalOffset),
            MediaKey = reader.GetString(1 + ordinalOffset),
            PostId = reader.GetString(0 + ordinalOffset),
            RelativePath = reader.IsDBNull(4 + ordinalOffset) ? null : reader.GetString(4 + ordinalOffset),
            SourceUrl = reader.GetString(3 + ordinalOffset),
            Width = reader.IsDBNull(6 + ordinalOffset) ? null : reader.GetInt32(6 + ordinalOffset),
        };
    }

    private static ArchivedPostRecord ReadPost(SqliteDataReader reader)
    {
        return new ArchivedPostRecord
        {
            ConversationId = reader.IsDBNull(7) ? null : reader.GetString(7),
            CreatedAtUtc = DateTimeOffset.Parse(reader.GetString(5), null, System.Globalization.DateTimeStyles.RoundtripKind),
            InReplyToUserId = reader.IsDBNull(8) ? null : reader.GetString(8),
            LikeCount = reader.GetInt32(9),
            MetadataRelativePath = reader.IsDBNull(14) ? null : reader.GetString(14),
            PostId = reader.GetString(0),
            PostType = (ArchivePostType)reader.GetInt32(6),
            ProfileId = Guid.Parse(reader.GetString(1)),
            QuoteCount = reader.GetInt32(12),
            ReplyCount = reader.GetInt32(10),
            RepostCount = reader.GetInt32(11),
            Text = reader.GetString(4),
            TextRelativePath = reader.IsDBNull(13) ? null : reader.GetString(13),
            UserId = reader.GetString(2),
            Username = reader.GetString(3),
        };
    }

    private static async Task<Dictionary<string, List<ArchivedMediaRecord>>> LoadMediaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using SqliteCommand mediaCommand = connection.CreateCommand();
        mediaCommand.CommandText = """
            SELECT PostId, MediaKey, Kind, SourceUrl, RelativePath, IsPartial, Width, Height, DurationMs
            FROM Media;
            """;

        Dictionary<string, List<ArchivedMediaRecord>> mediaByPostId = [];
        await using SqliteDataReader reader = await mediaCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            ArchivedMediaRecord media = ReadMedia(reader);

            if (!mediaByPostId.TryGetValue(media.PostId, out List<ArchivedMediaRecord>? mediaList))
            {
                mediaList = [];
                mediaByPostId[media.PostId] = mediaList;
            }

            mediaList.Add(media);
        }

        return mediaByPostId;
    }
}
