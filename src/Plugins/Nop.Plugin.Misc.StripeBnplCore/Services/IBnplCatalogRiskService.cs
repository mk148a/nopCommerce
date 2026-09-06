namespace Nop.Plugin.Misc.StripeBnplCore.Services;

public interface IBnplCatalogRiskService
{
    Task<string> FindRestrictedTermAsync(int productId, string attributesXml = null);
}
