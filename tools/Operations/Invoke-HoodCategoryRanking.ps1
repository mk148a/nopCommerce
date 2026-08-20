[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SiteRoot,

    [ValidateSet('Preview', 'Apply')]
    [string]$Mode = 'Preview'
)

$ErrorActionPreference = 'Stop'

function Get-NopConnectionString([string]$Path) {
    $configPath = Join-Path $Path 'App_Data\appsettings.json'
    $config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
    $queue = [System.Collections.Generic.Queue[object]]::new()
    $queue.Enqueue($config)

    while ($queue.Count -gt 0) {
        $node = $queue.Dequeue()
        if ($node -isnot [pscustomobject]) { continue }
        foreach ($property in $node.PSObject.Properties) {
            if ($property.Name -match 'ConnectionString' -and $property.Value -is [string]) {
                return $property.Value
            }
            if ($property.Value -isnot [string] -and $null -ne $property.Value) {
                $queue.Enqueue($property.Value)
            }
        }
    }

    throw 'No SQL connection string was found in App_Data\\appsettings.json.'
}

function Get-ConnectionPart([string]$ConnectionString, [string[]]$Names) {
    foreach ($part in $ConnectionString -split ';') {
        foreach ($name in $Names) {
            if ($part -match ('^\s*' + [regex]::Escape($name) + '\s*=\s*(.*)$')) {
                return $Matches[1].Trim().Trim('"')
            }
        }
    }
}

function Invoke-Sql([string]$Sql) {
    $connectionString = Get-NopConnectionString $SiteRoot
    $server = Get-ConnectionPart $connectionString @('Server', 'Data Source')
    $database = Get-ConnectionPart $connectionString @('Database', 'Initial Catalog')
    $user = Get-ConnectionPart $connectionString @('User ID', 'UID', 'User')
    $password = Get-ConnectionPart $connectionString @('Password', 'PWD')

    try {
        $env:SQLCMDPASSWORD = $password
        & sqlcmd -S $server -d $database -U $user -C -W -s '|' -Q $Sql
        if ($LASTEXITCODE -ne 0) { throw "sqlcmd exited with code $LASTEXITCODE." }
    }
    finally {
        Remove-Item Env:SQLCMDPASSWORD -ErrorAction SilentlyContinue
    }
}

$rankingCte = @'
;WITH Sales AS (
    SELECT oi.ProductId, SUM(oi.Quantity) AS UnitsSold
    FROM OrderItem oi
    INNER JOIN [Order] o ON o.Id = oi.OrderId
    WHERE o.Deleted = 0
      AND o.OrderStatusId IN (20, 30) -- Processing and Complete only
    GROUP BY oi.ProductId
), Reviews AS (
    SELECT ProductId, COUNT(*) AS ReviewCount, AVG(CONVERT(decimal(9,4), Rating)) AS AverageRating
    FROM ProductReview
    WHERE IsApproved = 1
    GROUP BY ProductId
), Ranked AS (
    SELECT pcm.Id AS MappingId,
           pcm.CategoryId,
           pcm.ProductId,
           ISNULL(s.UnitsSold, 0) AS UnitsSold,
           ISNULL(r.ReviewCount, 0) AS ReviewCount,
           ISNULL(r.AverageRating, 0) AS AverageRating,
           -- A product can be mapped to a parent and a child category. Keep
           -- the same popularity rank on every mapping so parent category
           -- lists cannot pick an arbitrary child-category display order.
           DENSE_RANK() OVER (
               ORDER BY ISNULL(s.UnitsSold, 0) DESC,
                        ISNULL(r.ReviewCount, 0) DESC,
                        ISNULL(r.AverageRating, 0) DESC,
                        pcm.ProductId ASC) * 10 AS NewDisplayOrder
    FROM Product_Category_Mapping pcm
    INNER JOIN Product p ON p.Id = pcm.ProductId
    INNER JOIN Category c ON c.Id = pcm.CategoryId
    LEFT JOIN Sales s ON s.ProductId = pcm.ProductId
    LEFT JOIN Reviews r ON r.ProductId = pcm.ProductId
    WHERE p.Deleted = 0
      AND p.Published = 1
      AND c.Deleted = 0
      AND c.Published = 1
)
'@

if ($Mode -eq 'Preview') {
    Invoke-Sql @"
SET NOCOUNT ON;
$rankingCte
SELECT TOP 100 CategoryId, ProductId, UnitsSold, ReviewCount, AverageRating, NewDisplayOrder
FROM Ranked
ORDER BY CategoryId, NewDisplayOrder;
"@
    exit 0
}

$stamp = (Get-Date).ToUniversalTime().ToString('yyyyMMddHHmmss')
$backupTable = "HoodCategoryRankingBackup_$stamp"

Invoke-Sql @"
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

SELECT SYSUTCDATETIME() AS BackupCreatedOnUtc, Id, CategoryId, ProductId, IsFeaturedProduct, DisplayOrder
INTO [$backupTable]
FROM Product_Category_Mapping;

$rankingCte
SELECT MappingId, NewDisplayOrder
INTO #HoodCategoryRanking
FROM Ranked;

UPDATE pcm
SET DisplayOrder = ranking.NewDisplayOrder
FROM Product_Category_Mapping pcm
INNER JOIN #HoodCategoryRanking ranking ON ranking.MappingId = pcm.Id
WHERE pcm.DisplayOrder <> ranking.NewDisplayOrder;

SELECT @@ROWCOUNT AS UpdatedMappings, N'$backupTable' AS BackupTable;
COMMIT TRANSACTION;
"@

Write-Host "Ranking applied. Rollback source table: $backupTable" -ForegroundColor Green
