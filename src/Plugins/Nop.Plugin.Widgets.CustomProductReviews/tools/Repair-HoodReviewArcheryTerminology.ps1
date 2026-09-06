param(
  [Parameter(Mandatory = $true)]
  [string]$SiteRoot,

  [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

function Get-NopConnectionString([string]$root) {
  $file = Join-Path $root 'App_Data\appsettings.json'
  $json = Get-Content -LiteralPath $file -Raw | ConvertFrom-Json
  $value = [string]$json.ConnectionStrings.ConnectionString
  if ([string]::IsNullOrWhiteSpace($value)) {
    throw "No SQL connection was found in $file."
  }

  return ($value -replace '(?i)Trust Server Certificate\s*=', 'TrustServerCertificate=')
}

function Invoke-NonQuery([string]$connectionString, [string]$sql, $parameters = @{}) {
  $connection = [System.Data.SqlClient.SqlConnection]::new($connectionString)
  $command = $connection.CreateCommand()
  $command.CommandText = $sql
  $command.CommandTimeout = 300
  foreach ($entry in $parameters.GetEnumerator()) {
    [void]$command.Parameters.Add($entry.Key, [System.Data.SqlDbType]::NVarChar, -1)
    $command.Parameters[$entry.Key].Value = [string]$entry.Value
  }

  $connection.Open()
  try { return $command.ExecuteNonQuery() }
  finally { $connection.Dispose() }
}

$connectionString = Get-NopConnectionString $SiteRoot

# These are reader-facing terms, not raw machine-translation substitutions.
# `zihgir` is the Turkish archery term; nock stays technical and koç stays
# a product/style name where the original review uses it.
$updates = @(
  [pscustomobject]@{ ReviewId = 10; LanguageId = 2; LocaleKey = 'ReviewText'; Value = 'Zihgirimi aldım, şimdi denemeye gidiyorum! Çok teşekkür ederim!' }
  [pscustomobject]@{ ReviewId = 123; LanguageId = 2; LocaleKey = 'Title'; Value = 'Keşke tüm zihgirlerim de böyle otursaydı' }
  [pscustomobject]@{ ReviewId = 123; LanguageId = 2; LocaleKey = 'ReviewText'; Value = "Öncelikle müşteri hizmetlerini övmek istiyorum. Zihgirimi sipariş ettiğimde, Murat her şeyin doğru olduğundan emin olmak için benimle iletişime geçti. Nasıl başardıklarını bilmiyorum ama bu zihgir o kadar iyi oturuyor ve o kadar rahat ki, sanki zanaatkâr çalışırken ben de yanında durup deniyormuşum gibi.`nBu benim ikinci zihgirim. İlk zihgirim boynuzdandı ve onu çok seviyordum ama köpeklerim yedi. Köpekleriniz varsa boynuz zihgiri ortalıkta bırakmayın!`nİkinci zihgirim koyu mavi kehribardan; atış yaparken hem rahat hem de çok çekici." }
  [pscustomobject]@{ ReviewId = 1922; LanguageId = 2; LocaleKey = 'ReviewText'; Value = 'Güzel bir zihgir. Az önce atış poligonuna götürdüm. Bırakışı güzel ve takması rahat. Ellerim iri ama ölçü seçiminde çok yardımcı oldular. Teslim süresi de gayet iyiydi. Nadia''ya ve HoodArchery ekibine teşekkürler!!' }
  [pscustomobject]@{ ReviewId = 1929; LanguageId = 2; LocaleKey = 'ReviewText'; Value = 'Zihgir tam olarak uydu ve takması çok hoş, çok teşekkür ederim!' }
  [pscustomobject]@{ ReviewId = 2010; LanguageId = 2; LocaleKey = 'ReviewText'; Value = 'Bu zihgirlere bayılıyorum. Şu anda dört tane var. Hem çok güzeller hem de işlevleri gayet iyi. Oklar zihgir yüzeyinden pürüzsüzce kayıp hedeflerini vuruyor!' }
  [pscustomobject]@{ ReviewId = 2137; LanguageId = 2; LocaleKey = 'ReviewText'; Value = 'Okunu düzgün biçimde kavramanızı sağlayan çok güzel, küçük bir zihgir. Yay ipi, oluk olmadan zihgir tarafından tutuluyor. Gayet iyi çalışıyor! Ölçü tam oturdu! Teşekkürler! ☺️👍🏻' }
  [pscustomobject]@{ ReviewId = 2296; LanguageId = 2; LocaleKey = 'ReviewText'; Value = 'Bu oklar harika. Osmanlı tarzı tüy süslemeli ve koç nocklu olanlarını aldım. Zihgir kullanımı için mükemmel.' }
  [pscustomobject]@{ ReviewId = 2296; LanguageId = 5; LocaleKey = 'ReviewText'; Value = 'یہ تیر شاندار ہیں۔ میں نے انہیں عثمانی پرکاری اور «koç» ناک کے ساتھ لیا۔ انگوٹھے کی انگوٹھی کے ساتھ استعمال کے لیے بہترین ہیں۔' }
  [pscustomobject]@{ ReviewId = 2296; LanguageId = 9; LocaleKey = 'ReviewText'; Value = 'Diese Pfeile sind großartig. Ich habe sie mit osmanischer Befiederung und einem „koç“-Nock gekauft. Perfekt für die Verwendung mit einem Daumenring.' }
  [pscustomobject]@{ ReviewId = 2296; LanguageId = 10; LocaleKey = 'ReviewText'; Value = 'Ces flèches sont superbes. Je les ai achetées avec un empennage ottoman et une encoche « koç ». Elles sont parfaites avec un anneau de pouce.' }
  [pscustomobject]@{ ReviewId = 2296; LanguageId = 11; LocaleKey = 'ReviewText'; Value = 'Queste frecce sono fantastiche. Le ho prese con impennatura ottomana e cocca «koç». Perfette da usare con un anello da pollice.' }
  [pscustomobject]@{ ReviewId = 2296; LanguageId = 12; LocaleKey = 'ReviewText'; Value = 'Disse pile er fantastiske. Jeg har købt dem med osmannisk fjerbesætning og en „koç“-nock. De er perfekte til brug med en tommelfingerring.' }
  [pscustomobject]@{ ReviewId = 2296; LanguageId = 13; LocaleKey = 'ReviewText'; Value = 'Deze pijlen zijn geweldig. Ik heb ze gekocht met Ottomaanse veren en een "koç"-nok. Perfect voor gebruik met een duimring.' }
  [pscustomobject]@{ ReviewId = 2296; LanguageId = 14; LocaleKey = 'ReviewText'; Value = 'Ezek a nyilak csodálatosak. Oszmán tollazattal és „koç” nockkal rendeltem őket. Tökéletesek hüvelykujjgyűrűvel való használatra.' }
  [pscustomobject]@{ ReviewId = 2296; LanguageId = 15; LocaleKey = 'ReviewText'; Value = 'Disse pilene er fantastiske. Jeg kjøpte dem med osmansk fjærdekning og en «koç»-nock. Perfekte til bruk med tommelfingerring.' }
  [pscustomobject]@{ ReviewId = 2296; LanguageId = 16; LocaleKey = 'ReviewText'; Value = 'Te strzały są wspaniałe. Kupiłem je z piórami w stylu osmańskim i nockiem „koç”. Idealnie nadają się do używania z pierścieniem na kciuk.' }
  [pscustomobject]@{ ReviewId = 2296; LanguageId = 17; LocaleKey = 'ReviewText'; Value = 'Estas setas são fantásticas. Comprei-as com empenagem otomana e um nock «koç». São perfeitas para usar com um anel de polegar.' }
  [pscustomobject]@{ ReviewId = 2296; LanguageId = 18; LocaleKey = 'ReviewText'; Value = 'Săgețile acestea sunt minunate. Le-am cumpărat cu penaj otoman și cu un nock „koç”. Sunt perfecte pentru folosirea cu un inel pentru degetul mare.' }
  [pscustomobject]@{ ReviewId = 2296; LanguageId = 19; LocaleKey = 'ReviewText'; Value = 'Estas flechas son maravillosas. Las compré con emplumado otomano y culatín «koç». Son perfectas para usar con un anillo para el pulgar.' }
  [pscustomobject]@{ ReviewId = 2296; LanguageId = 20; LocaleKey = 'ReviewText'; Value = 'De här pilarna är fantastiska. Jag köpte dem med osmansk fjäderklädsel och en ”koç”-nock. Perfekta att använda med en tumring.' }
  [pscustomobject]@{ ReviewId = 2296; LanguageId = 21; LocaleKey = 'ReviewText'; Value = 'Αυτά τα βέλη είναι υπέροχα. Τα αγόρασα με οθωμανικό φτέρωμα και nock «koç». Είναι ιδανικά για χρήση με δαχτυλίδι αντίχειρα.' }
  [pscustomobject]@{ ReviewId = 2296; LanguageId = 22; LocaleKey = 'ReviewText'; Value = 'Anak panah ini hebat. Saya mendapatkannya dengan bulu gaya Uthmaniyyah dan nock “koç”. Sesuai digunakan dengan cincin ibu jari.' }
  [pscustomobject]@{ ReviewId = 2296; LanguageId = 23; LocaleKey = 'ReviewText'; Value = 'これらの矢は素晴らしいです。オスマン風の羽根飾りと「koç」ノック付きのものを購入しました。親指リングで使うのに最適です。' }
  [pscustomobject]@{ ReviewId = 2296; LanguageId = 24; LocaleKey = 'ReviewText'; Value = 'هذه السهام رائعة. اشتريتها بريش على الطراز العثماني وبنقرة «koç». وهي مثالية للاستخدام مع خاتم الإبهام.' }
  [pscustomobject]@{ ReviewId = 2296; LanguageId = 25; LocaleKey = 'ReviewText'; Value = 'Эти стрелы просто замечательные. Я купил их с османским оперением и хвостовиком «koç». Идеально подходят для использования с кольцом для большого пальца.' }
)

$reviewIds = @($updates.ReviewId | Sort-Object -Unique)
$backupTable = 'HoodReviewTerminologyBackup_' + (Get-Date).ToUniversalTime().ToString('yyyyMMddHHmmss')
$idList = $reviewIds -join ','

if ($DryRun) {
  Write-Host "Dry run: $($updates.Count) reviewed terminology updates for review IDs $idList." -ForegroundColor Cyan
  exit 0
}

Invoke-NonQuery $connectionString @"
SELECT SYSUTCDATETIME() AS BackupCreatedOnUtc, *
INTO [$backupTable]
FROM LocalizedProperty
WHERE LocaleKeyGroup = N'ProductReview'
  AND EntityId IN ($idList)
  AND ((LanguageId = 2) OR EntityId = 2296);
"@ | Out-Null

$updated = 0
foreach ($update in $updates) {
  $sql = @'
UPDATE LocalizedProperty
SET LocaleValue = @LocaleValue
WHERE EntityId = @EntityId
  AND LanguageId = @LanguageId
  AND LocaleKeyGroup = N'ProductReview'
  AND LocaleKey = @LocaleKey;
'@
  $connection = [System.Data.SqlClient.SqlConnection]::new($connectionString)
  $command = $connection.CreateCommand()
  $command.CommandText = $sql
  [void]$command.Parameters.Add('@EntityId', [System.Data.SqlDbType]::Int)
  [void]$command.Parameters.Add('@LanguageId', [System.Data.SqlDbType]::Int)
  [void]$command.Parameters.Add('@LocaleKey', [System.Data.SqlDbType]::NVarChar, 400)
  [void]$command.Parameters.Add('@LocaleValue', [System.Data.SqlDbType]::NVarChar, -1)
  $command.Parameters['@EntityId'].Value = $update.ReviewId
  $command.Parameters['@LanguageId'].Value = $update.LanguageId
  $command.Parameters['@LocaleKey'].Value = $update.LocaleKey
  $command.Parameters['@LocaleValue'].Value = $update.Value
  $connection.Open()
  try {
    if ($command.ExecuteNonQuery() -ne 1) {
      throw "Expected exactly one ProductReview localized value for review $($update.ReviewId), language $($update.LanguageId), key $($update.LocaleKey)."
    }
    $updated++
  } finally { $connection.Dispose() }
}

Write-Host "Backup table: $backupTable" -ForegroundColor Cyan
Write-Host "Reviewed terminology rows updated: $updated" -ForegroundColor Green
