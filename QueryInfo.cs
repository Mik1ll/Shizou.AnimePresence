﻿using System.Web;

namespace Shizou.AnimePresence;

public class QueryInfo
{
    private QueryInfo()
    {
    }

    public static QueryInfo? GetQueryInfo(string path, bool allowRestricted)
    {
        if (!Uri.TryCreate(path, UriKind.Absolute, out var uri) || !new[] { Uri.UriSchemeHttp, Uri.UriSchemeHttps }.Contains(uri.Scheme))
        {
            Console.WriteLine("Path is not an HTTP(S) URL");
            return null;
        }

        var fileQuery = HttpUtility.ParseQueryString(uri.Query);
        if (!string.Equals(fileQuery.Get("appId"), Program.AppId, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("The app id is not present or does not match");
            return null;
        }

        var restrictedSpecified = bool.TryParse(fileQuery.Get("restricted"), out var restricted);

        if (!allowRestricted)
        {
            if (!restrictedSpecified)
            {
                Console.Error.WriteLine("Restricted is a required parameter when restricted files are disallowed");
                return null;
            }

            if (restricted)
            {
                Console.Error.WriteLine("Not allowing restricted file");
                return null;
            }
        }

        if ((fileQuery.Get("epNo") ?? fileQuery.Get("episodeNumber")) is not { } episodeNumber)
        {
            Console.Error.WriteLine("Episode number is a required parameter");
            return null;
        }

        var animeId = fileQuery.Get("animeId");
        if (fileQuery.Get("animeName") is not { } animeName)
        {
            Console.Error.WriteLine("Anime name is a required parameter");
            return null;
        }

        var episodeType = (fileQuery.Get("epType") ?? fileQuery.Get("episodeType")) ?? episodeNumber[0] switch
        {
            'S' => "Special",
            'C' => "Credit",
            'T' => "Trailer",
            'P' => "Parody",
            'O' => "Other",
            _ => "Episode"
        };

        // Don't show poster if it could be NSFW
        var posterUrl = restrictedSpecified && !restricted
            ? fileQuery.Get("posterUrl") ??
              (fileQuery.Get("posterFilename") is { } filename
                  ? $"https://cdn.anidb.net/images/main/{filename}"
                  : null)
            : null;

        return new QueryInfo
        {
            EpisodeName = fileQuery.Get("episodeName"),
            AnimeId = animeId,
            AnimeName = animeName,
            EpisodeNumber = episodeNumber,
            EpisodeType = episodeType,
            EpisodeCount = fileQuery.Get("epCount") ?? fileQuery.Get("episodeCount"),
            AnimeUrl = fileQuery.Get("animeUrl") ?? (animeId is null ? null : $"https://anidb.net/anime/{animeId}"),
            PosterUrl = posterUrl
        };
    }

    public string? AnimeId { get; init; }
    public required string AnimeName { get; init; }
    public required string EpisodeNumber { get; init; }
    public required string EpisodeType { get; init; }
    public string? EpisodeName { get; init; }
    public string? EpisodeCount { get; init; }
    public string? AnimeUrl { get; init; }
    public string? PosterUrl { get; init; }
}