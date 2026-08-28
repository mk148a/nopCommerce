using System;
using System.Collections.Generic;

namespace Nop.Plugin.Widgets.CustomProductReviews;

/// <summary>
/// Provides the storefront resources owned by CustomProductReviews and the narrowly
/// verified core-resource corrections required by the review form.
/// </summary>
internal static class ReviewLocalizationResources
{
    /// <summary>
    /// Gets a fresh, case-insensitive resource dictionary for an active storefront culture or code.
    /// </summary>
    internal static Dictionary<string, string> GetResources(string languageCode)
    {
        var code = NormalizeLanguageCode(languageCode);
        var values = GetPluginResources(code);

        if (GetCoreCorrectionOverrides(code) is { } overrides)
        {
            foreach (var (key, value) in overrides)
                values[key] = value;
        }

        return values;
    }

    /// <summary>
    /// Gets only resources owned by this plugin. The plugin install/update path
    /// uses this catalog so it cannot repeatedly overwrite administrator-owned
    /// nopCommerce core translations. Verified core corrections are applied by
    /// the one-time, backup-aware repair tool through <see cref="GetResources"/>.
    /// </summary>
    internal static Dictionary<string, string> GetPluginResources(string languageCode) =>
        GetPluginOwnedResources(NormalizeLanguageCode(languageCode));

    private static Dictionary<string, string> GetPluginOwnedResources(string code) => code switch
    {
        "tr" => Create(
            "Ürün yorumları:",
            "Fotoğraf veya kısa bir video ekleyin",
            "Fotoğraflar veya kısa bir video, yorumunuzun diğer okçular için daha yararlı olmasını sağlar.",
            "En fazla {0} dosya. Fotoğraflar en fazla {1} MB; kısa videolar en fazla {2} MB. JPG, PNG, WebP, MP4, MOV veya WebM.",
            "Seçilen dosyalar", "Desteklenmeyen dosya türü.", "Seçilen dosya sayısı fazla.", "Seçilen dosya çok büyük.",
            "Yorum gönderiliyor...", "Yorumunuzu gönderemedik. Lütfen tekrar deneyin.", "Oyunuzu kaydedemedik. Lütfen tekrar deneyin."),
        "de" => Create(
            "Produktbewertungen für",
            "Füge Fotos oder ein kurzes Video hinzu",
            "Fotos oder ein kurzes Video machen deine Bewertung für andere Bogenschützen noch hilfreicher.",
            "Bis zu {0} Dateien. Fotos mit einer Größe von jeweils bis zu {1} MB; kurze Videos mit einer Größe von bis zu {2} MB. JPG, PNG, WebP, MP4, MOV oder WebM.",
            "Ausgewählte Dateien", "Nicht unterstützter Dateityp.", "Es wurden zu viele Dateien ausgewählt.", "Die ausgewählte Datei ist zu groß.",
            "Bewertung wird gesendet...", "Wir konnten Ihre Bewertung nicht übermitteln. Bitte versuchen Sie es erneut.", "Ihre Stimme konnte nicht erfasst werden. Bitte versuchen Sie es erneut."),
        "fr" => Create(
            "Avis clients pour",
            "Ajoutez des photos ou une courte vidéo",
            "Des photos ou une courte vidéo rendront votre avis plus utile pour les autres archers.",
            "Jusqu'à {0} fichiers. Photos d'une taille maximale de {1} Mo chacune ; courtes vidéos d'une taille maximale de {2} Mo. Formats pris en charge : JPG, PNG, WebP, MP4, MOV ou WebM.",
            "Fichiers sélectionnés", "Type de fichier non pris en charge.", "Trop de fichiers sélectionnés.", "Le fichier sélectionné est trop volumineux.",
            "Envoi de l'avis...", "Nous n'avons pas pu envoyer votre avis. Veuillez réessayer.", "Nous n'avons pas pu enregistrer votre vote. Veuillez réessayer."),
        "es" => Create(
            "Opiniones de productos para",
            "Añade fotos o un vídeo breve",
            "Las fotos o un vídeo breve hacen que tu reseña resulte más útil para otros arqueros.",
            "Hasta {0} archivos. Fotos de hasta {1} MB cada una; vídeos cortos de hasta {2} MB. Formatos JPG, PNG, WebP, MP4, MOV o WebM.",
            "Archivos seleccionados", "Tipo de archivo no compatible.", "Se han seleccionado demasiados archivos.", "El archivo seleccionado es demasiado grande.",
            "Enviando la reseña...", "No hemos podido enviar tu reseña. Inténtalo de nuevo.", "No hemos podido registrar tu voto. Inténtalo de nuevo, por favor."),
        "it" => Create(
            "Recensioni del prodotto per",
            "Aggiungi delle foto o un breve video",
            "Aggiungere delle foto o un breve video renderà la tua recensione più utile per gli altri arcieri.",
            "Fino a {0} file. Foto fino a {1} MB ciascuna; brevi video fino a {2} MB. JPG, PNG, WebP, MP4, MOV o WebM.",
            "File selezionati", "Tipo di file non supportato.", "Sono stati selezionati troppi file.", "Il file selezionato è troppo grande.",
            "Invio della recensione...", "Non siamo riusciti a inviare la tua recensione. Prova di nuovo.", "Non siamo riusciti a registrare il tuo voto. Prova di nuovo."),
        "pt" => Create(
            "Avaliações do produto para",
            "Adicionar fotos ou um vídeo curto",
            "As fotografias ou um pequeno vídeo tornam a sua avaliação mais útil para outros arqueiros.",
            "Até {0} ficheiros. Fotografias com um tamanho máximo de {1} MB cada; vídeos curtos com um tamanho máximo de {2} MB. JPG, PNG, WebP, MP4, MOV ou WebM.",
            "Ficheiros selecionados", "Tipo de ficheiro não suportado.", "Foram selecionados demasiados ficheiros.", "O ficheiro selecionado é demasiado grande.",
            "A enviar a avaliação...", "Não foi possível enviar a sua avaliação. Por favor, tente novamente.", "Não foi possível registar o seu voto. Por favor, tente novamente."),
        "nl" => Create(
            "Productbeoordelingen voor",
            "Voeg foto’s of een korte video toe",
            "Met foto’s of een korte video wordt je beoordeling nuttiger voor andere boogschutters.",
            "Maximaal {0} bestanden. Foto’s van maximaal {1} MB per stuk; korte video’s van maximaal {2} MB. JPG, PNG, WebP, MP4, MOV of WebM.",
            "Geselecteerde bestanden", "Bestandstype wordt niet ondersteund.", "Er zijn te veel bestanden geselecteerd.", "Het geselecteerde bestand is te groot.",
            "Beoordeling wordt verzonden...", "We konden je recensie niet verzenden. Probeer het nog eens.", "We hebben uw stem niet kunnen registreren. Probeer het alstublieft nog eens."),
        "da" => Create(
            "Produktanmeldelser for",
            "Tilføj billeder eller en kort video",
            "Billeder eller en kort video gør din anmeldelse mere nyttig for andre bueskytter.",
            "Op til {0} filer. Fotos på op til {1} MB hver; korte videoer på op til {2} MB. JPG, PNG, WebP, MP4, MOV eller WebM.",
            "Udvalgte filer", "Filtype understøttes ikke.", "Der er valgt for mange filer.", "Den valgte fil er for stor.",
            "Anmeldelsen sendes...", "Det var ikke muligt at indsende din anmeldelse. Prøv venligst igen.", "Vi kunne ikke registrere din stemme. Prøv venligst igen."),
        "sv" => Create(
            "Produktrecensioner för",
            "Lägg till bilder eller en kort video",
            "Bilder eller en kort video gör din recension mer användbar för andra bågskyttar.",
            "Upp till {0} filer. Foton på upp till {1} MB vardera; korta videoklipp på upp till {2} MB. JPG, PNG, WebP, MP4, MOV eller WebM.",
            "Valda filer", "Filtyp som inte stöds.", "För många filer har valts.", "Den valda filen är för stor.",
            "Skickar in recension...", "Vi kunde inte skicka in din recension. Försök igen.", "Vi kunde inte registrera din röst. Försök igen."),
        "hu" => Create(
            "Termékértékelések ehhez:",
            "Tölts fel fotókat vagy egy rövid videót",
            "A fotók vagy egy rövid videó segítségével véleményed hasznosabbá válik a többi íjász számára.",
            "Legfeljebb {0} fájl. Fotók: egyenként legfeljebb {1} MB; rövid videók: legfeljebb {2} MB. JPG, PNG, WebP, MP4, MOV vagy WebM formátumban.",
            "Kiválasztott fájlok", "Nem támogatott fájltípus.", "Túl sok fájlt választott ki.", "A kiválasztott fájl túl nagy.",
            "A vélemény elküldése...", "Nem sikerült elküldeni a véleményét. Kérjük, próbálja meg újra!", "Nem sikerült rögzítenünk a szavazatát. Kérjük, próbálja meg újra."),
        "nn" => Create(
            "Produktanmeldingar for",
            "Legg til bilete eller ein kort video",
            "Bilete eller ein kort video gjer meldinga di meir nyttig for andre bogeskyttarar.",
            "Opp til {0} filer. Bilete på opp til {1} MB kvar; korte videoar på opp til {2} MB. JPG, PNG, WebP, MP4, MOV eller WebM.",
            "Valde filer", "Ikkje støtta filtype.", "For mange filer er valde.", "Den valde fila er for stor.",
            "Sender inn omtalen...", "Vi klarte ikkje å sende inn omtalen din. Prøv igjen.", "Vi klarte ikkje å registrere røysta di. Prøv igjen."),
        "pl" => Create(
            "Opinie o produkcie:",
            "Dodaj zdjęcia lub krótki filmik",
            "Zdjęcia lub krótki film sprawią, że Twoja recenzja będzie bardziej przydatna dla innych łuczników.",
            "Maksymalnie {0} plików. Zdjęcia o rozmiarze do {1} MB każde; krótkie filmy o rozmiarze do {2} MB. Formaty: JPG, PNG, WebP, MP4, MOV lub WebM.",
            "Wybrane pliki", "Nieobsługiwany typ pliku.", "Wybrano zbyt wiele plików.", "Wybrany plik jest zbyt duży.",
            "Wysyłanie recenzji...", "Nie udało nam się opublikować Twojej recenzji. Spróbuj ponownie.", "Nie udało nam się zarejestrować Twojego głosu. Spróbuj ponownie."),
        "ro" => Create(
            "Recenzii pentru produsul",
            "Adaugă fotografii sau un videoclip scurt",
            "Fotografiile sau un scurt videoclip fac ca recenzia ta să fie mai utilă pentru ceilalți arcași.",
            "Până la {0} fișiere. Fotografii de până la {1} MB fiecare; videoclipuri scurte de până la {2} MB. Formate: JPG, PNG, WebP, MP4, MOV sau WebM.",
            "Fișiere selectate", "Tip de fișier neacceptat.", "Au fost selectate prea multe fișiere.", "Fișierul selectat este prea mare.",
            "Se trimite recenzia...", "Nu am putut trimite recenzia ta. Te rugăm să încerci din nou.", "Nu am putut înregistra votul dumneavoastră. Vă rugăm să încercați din nou."),
        "el" => Create(
            "Κριτικές προϊόντων για",
            "Προσθέστε φωτογραφίες ή ένα σύντομο βίντεο",
            "Οι φωτογραφίες ή ένα σύντομο βίντεο καθιστούν την κριτική σας πιο χρήσιμη για τους άλλους τοξότες.",
            "Έως {0} αρχεία. Φωτογραφίες έως {1} MB η καθεμία· σύντομα βίντεο έως {2} MB. JPG, PNG, WebP, MP4, MOV ή WebM.",
            "Επιλεγμένα αρχεία", "Μη υποστηριζόμενος τύπος αρχείου.", "Έχουν επιλεγεί πάρα πολλά αρχεία.", "Το επιλεγμένο αρχείο είναι πολύ μεγάλο.",
            "Αποστολή κριτικής...", "Δεν καταφέραμε να υποβάλουμε την κριτική σας. Παρακαλώ, δοκιμάστε ξανά.", "Δεν καταφέραμε να καταγράψουμε την ψήφο σας. Παρακαλώ δοκιμάστε ξανά."),
        "ms" => Create(
            "Ulasan produk untuk",
            "Tambah foto atau video pendek",
            "Gambar atau video pendek menjadikan ulasan anda lebih membantu bagi pemanah lain.",
            "Sehingga {0} fail. Foto sehingga {1} MB setiap satu; video pendek sehingga {2} MB. JPG, PNG, WebP, MP4, MOV atau WebM.",
            "Fail terpilih", "Jenis fail tidak disokong.", "Terlalu banyak fail dipilih.", "Fail yang dipilih terlalu besar.",
            "Mengemukakan ulasan...", "Kami tidak dapat menghantar ulasan anda. Sila cuba lagi.", "Kami tidak dapat merekodkan undian anda. Sila cuba lagi."),
        "ja" => Create(
            "商品のレビュー：",
            "写真や短い動画を追加する",
            "写真や短い動画を添えると、他のアーチャーにとってあなたのレビューがより参考になります。",
            "最大{0}ファイルまで。写真は1枚あたり最大{1} MB、短い動画は最大{2} MBまで。JPG、PNG、WebP、MP4、MOV、またはWebM。",
            "選択されたファイル", "サポートされていないファイル形式です。", "選択したファイルが多すぎます。", "選択したファイルが大きすぎます。",
            "レビューを送信中...", "レビューを送信できませんでした。もう一度お試しください。", "投票を記録できませんでした。もう一度お試しください。"),
        "ru" => Create(
            "Отзывы о товаре:",
            "Добавьте фотографии или короткое видео",
            "Фотографии или короткое видео сделают ваш отзыв более полезным для других лучников.",
            "До {0} файлов. Фотографии размером до {1} МБ каждая; короткие видеоролики размером до {2} МБ. Форматы: JPG, PNG, WebP, MP4, MOV или WebM.",
            "Выбранные файлы", "Неподдерживаемый тип файла.", "Выбрано слишком много файлов.", "Выбранный файл слишком велик.",
            "Отправка отзыва...", "Нам не удалось отправить ваш отзыв. Пожалуйста, попробуйте ещё раз.", "Нам не удалось зарегистрировать ваш голос. Пожалуйста, попробуйте ещё раз."),
        "ur" => Create(
            "مصنوعات کے جائزے برائے",
            "تصاویر یا ایک مختصر ویڈیو شامل کریں۔",
            "تصاویر یا ایک مختصر ویڈیو آپ کے جائزے کو دوسرے تیر اندازوں کے لیے زیادہ مددگار بناتی ہیں۔",
            "{0} فائلوں تک۔ ہر تصویر {1} میگا بائٹ تک؛ مختصر ویڈیوز {2} میگا بائٹ تک۔ JPG، PNG، WebP، MP4، MOV یا WebM۔",
            "منتخب فائلیں", "غیر معاونت یافتہ فائل کی قسم۔", "بہت سی فائلیں منتخب کی گئی ہیں۔", "منتخب فائل بہت بڑی ہے۔",
            "جائزہ جمع ہو رہا ہے...", "ہم آپ کا جائزہ جمع نہیں کروا سکے۔ براہِ کرم دوبارہ کوشش کریں۔", "ہم آپ کا ووٹ ریکارڈ نہیں کر سکے۔ براہِ کرم دوبارہ کوشش کریں۔"),
        "ar" => Create(
            "مراجعات المنتج لـ",
            "أضف صورًا أو مقطع فيديو قصيرًا",
            "تساعد الصور أو مقطع الفيديو القصير في جعل تقييمك أكثر فائدة للرماة الآخرين.",
            "ما يصل إلى {0} ملفات. الصور بحجم يصل إلى {1} ميغابايت لكل منها؛ ومقاطع الفيديو القصيرة بحجم يصل إلى {2} ميغابايت. صيغ JPG، PNG، WebP، MP4، MOV أو WebM.",
            "ملفات مختارة", "نوع ملف غير مدعوم.", "تم تحديد عدد كبير جدًا من الملفات.", "حجم الملف المحدد كبير جدًّا.",
            "جاري إرسال التعليق...", "لم نتمكن من إرسال تقييمك. يرجى المحاولة مرة أخرى.", "لم نتمكن من تسجيل تصويتك. يرجى المحاولة مرة أخرى."),
        _ => Create(
            "Product reviews for",
            "Add photos or a short video",
            "Photos or a short video make your review more helpful for other archers.",
            "Up to {0} files. Photos up to {1} MB each; short videos up to {2} MB. JPG, PNG, WebP, MP4, MOV or WebM.",
            "Selected files", "Unsupported file type.", "Too many files selected.", "A selected file is too large.",
            "Submitting review...", "We could not submit your review. Please try again.", "We could not record your vote. Please try again.")
    };

    private static Dictionary<string, string> GetCoreCorrectionOverrides(string code) => code switch
    {
        "tr" => CreateOverrides(
            ("Reviews.From", "Yazan"),
            ("Reviews.AlreadyAddedProductReviews", "Bu ürün için zaten bir yorum eklendi.")),
        "ur" => CreateOverrides(
            ("Reviews.From", "کی طرف سے"),
            ("Reviews.Date", "تاریخ"),
            ("Reviews.Helpfulness.WasHelpful?", "کیا یہ جائزہ مددگار تھا؟"),
            ("Common.Yes", "ہاں"),
            ("Common.No", "نہیں"),
            ("Reviews.Fields.Title", "جائزے کا عنوان"),
            ("Reviews.Fields.ReviewText", "جائزے کا متن"),
            ("Reviews.Fields.Rating", "درجہ بندی"),
            ("Reviews.Fields.Rating.Bad", "خراب"),
            ("Reviews.Fields.Rating.NotGood", "اچھا نہیں"),
            ("Reviews.Fields.Rating.NotBadNotExcellent", "نہ برا، نہ بہترین"),
            ("Reviews.Fields.Rating.Good", "اچھا"),
            ("Reviews.Fields.Rating.Excellent", "بہترین"),
            ("Reviews.ExistingReviews", "موجودہ جائزے"),
            ("Reviews.SubmitButton", "جائزہ جمع کرائیں"),
            ("Reviews.Write", "اپنا جائزہ لکھیں"),
            ("Reviews.AlreadyAddedProductReviews", "اس پروڈکٹ کے لیے پہلے ہی ایک جائزہ شامل کیا جا چکا ہے۔"),
            ("Reviews.Reply", "منتظم نے اس جائزے کا جواب دیا ہے")),
        "da" => CreateOverrides(("Common.No", "Nej")),
        "pl" => CreateOverrides(("Common.No", "Nie")),
        "nn" => CreateOverrides(("Reviews.Helpfulness.WasHelpful?", "Var denne omtalen nyttig?")),
        "ar" => CreateOverrides(("Reviews.Fields.Rating", "التقييم")),
        "ro" => CreateOverrides(
            ("Reviews.AlreadyAddedProductReviews", "Pentru acest produs a fost deja adăugată o recenzie."),
            ("Reviews.Write", "Scrie propria recenzie")),
        _ => null
    };

    private static Dictionary<string, string> Create(
        string productReviewsFor,
        string mediaHeading,
        string mediaGuidance,
        string mediaRequirements,
        string mediaSelectedFiles,
        string mediaInvalidType,
        string mediaTooMany,
        string mediaTooLarge,
        string reviewSubmitting,
        string reviewSubmitError,
        string reviewVoteError) => new(StringComparer.OrdinalIgnoreCase)
        {
            ["Reviews.ProductReviewsFor"] = productReviewsFor,
            ["Plugins.Widgets.CustomProductReviews.Media.Heading"] = mediaHeading,
            ["Plugins.Widgets.CustomProductReviews.Media.Guidance"] = mediaGuidance,
            ["Plugins.Widgets.CustomProductReviews.Media.Requirements"] = mediaRequirements,
            ["Plugins.Widgets.CustomProductReviews.Media.SelectedFiles"] = mediaSelectedFiles,
            ["Plugins.Widgets.CustomProductReviews.Media.InvalidType"] = mediaInvalidType,
            ["Plugins.Widgets.CustomProductReviews.Media.TooMany"] = mediaTooMany,
            ["Plugins.Widgets.CustomProductReviews.Media.TooLarge"] = mediaTooLarge,
            ["Plugins.Widgets.CustomProductReviews.Review.Submitting"] = reviewSubmitting,
            ["Plugins.Widgets.CustomProductReviews.Review.SubmitError"] = reviewSubmitError,
            ["Plugins.Widgets.CustomProductReviews.Review.VoteError"] = reviewVoteError
        };

    private static Dictionary<string, string> CreateOverrides(params (string Key, string Value)[] values)
    {
        var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in values)
            dictionary[key] = value;

        return dictionary;
    }

    private static string NormalizeLanguageCode(string languageCode) => (languageCode ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "en" or "en-us" or "en-gb" or "en-ca" or "en-za" or "en-au" or "gb" or "ca" or "za" or "au" => "en",
        "tr" or "tr-tr" => "tr",
        "fr" or "fr-fr" => "fr",
        "de" or "de-de" => "de",
        "it" or "it-it" => "it",
        "es" or "es-es" => "es",
        "da" or "da-dk" or "dk" => "da",
        "sv" or "sv-se" or "se" => "sv",
        "nl" or "nl-nl" => "nl",
        "hu" or "hu-hu" => "hu",
        "nn" or "nn-no" or "no" or "nb" or "nb-no" => "nn",
        "pl" or "pl-pl" => "pl",
        "pt" or "pt-pt" => "pt",
        "ro" or "ro-ro" => "ro",
        "el" or "el-gr" or "gr" => "el",
        "ms" or "ms-my" or "my" => "ms",
        "ja" or "ja-jp" or "jp" => "ja",
        "ru" or "ru-ru" => "ru",
        "ur" or "ur-pk" => "ur",
        "ar" or "ar-001" => "ar",
        _ => "en"
    };
}
