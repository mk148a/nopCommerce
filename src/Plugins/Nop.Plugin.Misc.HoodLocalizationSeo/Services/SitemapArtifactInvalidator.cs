using System.Text.RegularExpressions;
using Nop.Core.Infrastructure;
using Nop.Services.Seo;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Services;

/// <summary>
/// Removes pre-generated XML sitemap artifacts after localized URLs change.
/// Files that are not produced by nopCommerce's sitemap filename format are
/// deliberately left untouched.
/// </summary>
public interface ISitemapArtifactInvalidator
{
    void InvalidateGeneratedXmlFiles();
}

public sealed class SitemapArtifactInvalidator : ISitemapArtifactInvalidator
{
    private const string SitemapSearchPattern = "sitemap-*.xml";
    private static readonly Regex GeneratedFileNamePattern = CreateGeneratedFileNamePattern();
    private readonly INopFileProvider _fileProvider;

    public SitemapArtifactInvalidator(INopFileProvider fileProvider)
    {
        _fileProvider = fileProvider;
    }

    public void InvalidateGeneratedXmlFiles()
    {
        var sitemapDirectory = _fileProvider.GetAbsolutePath(NopSeoDefaults.SitemapXmlDirectory);
        if (!_fileProvider.DirectoryExists(sitemapDirectory))
            return;

        foreach (var filePath in _fileProvider.GetFiles(sitemapDirectory, SitemapSearchPattern)
                     .Where(IsGeneratedSitemapXml))
        {
            try
            {
                _fileProvider.DeleteFile(filePath);
            }
            catch (Exception exception)
            {
                throw new IOException(
                    $"Failed to invalidate generated sitemap XML artifact '{filePath}'.", exception);
            }
        }
    }

    private bool IsGeneratedSitemapXml(string filePath)
    {
        var fileName = _fileProvider.GetFileName(filePath);
        return GeneratedFileNamePattern.IsMatch(fileName);
    }

    private static Regex CreateGeneratedFileNamePattern()
    {
        var escapedFormat = Regex.Escape(NopSeoDefaults.SitemapXmlFilePattern)
            .Replace(@"\{0}", @"\d+")
            .Replace(@"\{1}", @"\d+")
            .Replace(@"\{2}", @"\d+");

        return new Regex($@"\A{escapedFormat}\z",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    }
}
