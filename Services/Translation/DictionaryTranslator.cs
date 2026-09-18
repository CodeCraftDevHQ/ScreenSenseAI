using System.Text;
using System.Text.RegularExpressions;

namespace ScreenTranslator.Services.Translation;

/// <summary>
/// Fully offline, zero-configuration word-for-word translator. No server, no
/// API key, no internet. Quality is limited (no grammar, no reordering,
/// no context) but it always works, so it is the safe default that runs the
/// very first time someone presses the translate button.
/// </summary>
public sealed class DictionaryTranslator : ITranslator
{
    private static readonly Regex TokenPattern = new(@"[A-Za-z]+|[\u0600-\u06FF]+|[^A-Za-z\u0600-\u06FF]+", RegexOptions.Compiled);

    public string Name => "Offline dictionary";
    public bool IsConfigured => true;
    public bool SupportsOffline => true;

    public Task<IReadOnlyList<string>> TranslateAsync(IReadOnlyList<string> texts, string from, string to, CancellationToken token = default)
    {
        var table = ResolveTable(Normalize(from), Normalize(to));

        if (table is null)
        {
            throw new TranslationException(
                $"Offline dictionary only supports English <-> Persian right now (asked for {from} -> {to}).");
        }

        var results = new List<string>(texts.Count);

        foreach (var text in texts)
        {
            token.ThrowIfCancellationRequested();
            results.Add(TranslateOne(text, table));
        }

        return Task.FromResult<IReadOnlyList<string>>(results);
    }

    private static string TranslateOne(string text, IReadOnlyDictionary<string, string> table)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var builder = new StringBuilder();

        foreach (Match match in TokenPattern.Matches(text))
        {
            var token = match.Value;

            if (token.Length == 0 || !char.IsLetter(token[0]))
            {
                builder.Append(token);
                continue;
            }

            if (table.TryGetValue(token.ToLowerInvariant(), out var translated))
                builder.Append(ApplyCasing(token, translated));
            else
                builder.Append(token);
        }

        return builder.ToString();
    }

    private static string ApplyCasing(string original, string translated)
    {
        if (original.Length == 0 || translated.Length == 0)
            return translated;

        return char.IsUpper(original[0]) && char.IsLower(translated[0])
            ? char.ToUpperInvariant(translated[0]) + translated[1..]
            : translated;
    }

    private static string Normalize(string code)
    {
        var index = code.IndexOf('-');
        return (index > 0 ? code[..index] : code).ToLowerInvariant();
    }

    private static IReadOnlyDictionary<string, string>? ResolveTable(string from, string to)
    {
        if (from == "en" && to == "fa") return EnglishToPersian.Value;
        if (from == "fa" && to == "en") return PersianToEnglish.Value;
        return null;
    }

    private static readonly Lazy<IReadOnlyDictionary<string, string>> EnglishToPersian = new(BuildEnglishToPersian);

    private static readonly Lazy<IReadOnlyDictionary<string, string>> PersianToEnglish = new(() =>
    {
        var reversed = new Dictionary<string, string>();

        foreach (var pair in EnglishToPersian.Value)
        {
            if (!reversed.ContainsKey(pair.Value))
                reversed[pair.Value] = pair.Key;
        }

        return reversed;
    });

    private static IReadOnlyDictionary<string, string> BuildEnglishToPersian()
    {
        var pairs = new (string En, string Fa)[]
        {
            ("i", "من"), ("you", "تو"), ("he", "او"), ("she", "او"), ("it", "آن"), ("we", "ما"), ("they", "آن‌ها"),
            ("me", "من را"), ("my", "من"), ("your", "تو"), ("his", "او"), ("her", "او"), ("our", "ما"), ("their", "آن‌ها"),
            ("this", "این"), ("that", "آن"), ("these", "این‌ها"), ("those", "آن‌ها"),
            ("a", ""), ("an", ""), ("the", ""),
            ("and", "و"), ("or", "یا"), ("but", "اما"), ("if", "اگر"), ("because", "چون"), ("so", "پس"),
            ("is", "است"), ("are", "هستند"), ("was", "بود"), ("were", "بودند"), ("be", "بودن"), ("been", "بوده"),
            ("am", "هستم"), ("do", "انجام دادن"), ("does", "انجام می‌دهد"), ("did", "انجام داد"),
            ("have", "داشتن"), ("has", "دارد"), ("had", "داشت"), ("will", "خواهد"), ("would", "می‌شد"),
            ("can", "می‌تواند"), ("could", "می‌توانست"), ("should", "باید"), ("must", "باید"),
            ("not", "نه"), ("no", "نه"), ("yes", "بله"),
            ("hello", "سلام"), ("hi", "سلام"), ("goodbye", "خداحافظ"), ("bye", "خداحافظ"),
            ("please", "لطفاً"), ("thanks", "متشکرم"), ("thank", "متشکرم"), ("sorry", "ببخشید"), ("welcome", "خوش آمدید"),
            ("ok", "باشه"), ("okay", "باشه"),
            ("yes", "بله"), ("maybe", "شاید"),
            ("good", "خوب"), ("bad", "بد"), ("new", "جدید"), ("old", "قدیمی"), ("big", "بزرگ"), ("small", "کوچک"),
            ("fast", "سریع"), ("slow", "آهسته"), ("easy", "آسان"), ("hard", "سخت"), ("difficult", "دشوار"),
            ("hot", "داغ"), ("cold", "سرد"), ("high", "بالا"), ("low", "پایین"), ("open", "باز"), ("closed", "بسته"),
            ("free", "رایگان"), ("full", "پر"), ("empty", "خالی"), ("true", "درست"), ("false", "نادرست"),
            ("error", "خطا"), ("warning", "هشدار"), ("success", "موفقیت"), ("failed", "ناموفق"),
            ("ready", "آماده"), ("loading", "در حال بارگذاری"), ("saving", "در حال ذخیره"), ("saved", "ذخیره شد"),
            ("file", "فایل"), ("files", "فایل‌ها"), ("folder", "پوشه"), ("save", "ذخیره"), ("load", "بارگذاری"),
            ("open", "باز کردن"), ("close", "بستن"), ("delete", "حذف"), ("remove", "حذف"), ("add", "افزودن"),
            ("edit", "ویرایش"), ("new", "جدید"), ("create", "ایجاد"), ("update", "به‌روزرسانی"),
            ("settings", "تنظیمات"), ("options", "گزینه‌ها"), ("preferences", "ترجیحات"),
            ("language", "زبان"), ("translate", "ترجمه"), ("translation", "ترجمه"),
            ("search", "جستجو"), ("find", "پیدا کردن"), ("select", "انتخاب"), ("selected", "انتخاب شده"),
            ("home", "خانه"), ("back", "بازگشت"), ("next", "بعدی"), ("previous", "قبلی"), ("continue", "ادامه"),
            ("start", "شروع"), ("stop", "توقف"), ("pause", "مکث"), ("play", "پخش"), ("cancel", "لغو"),
            ("submit", "ارسال"), ("send", "ارسال"), ("receive", "دریافت"), ("download", "دانلود"), ("upload", "بارگذاری"),
            ("connect", "اتصال"), ("disconnect", "قطع اتصال"), ("online", "آنلاین"), ("offline", "آفلاین"),
            ("account", "حساب"), ("user", "کاربر"), ("username", "نام کاربری"), ("password", "رمز عبور"),
            ("login", "ورود"), ("logout", "خروج"), ("register", "ثبت‌نام"), ("email", "ایمیل"),
            ("name", "نام"), ("address", "آدرس"), ("phone", "تلفن"), ("number", "شماره"), ("date", "تاریخ"), ("time", "زمان"),
            ("today", "امروز"), ("tomorrow", "فردا"), ("yesterday", "دیروز"), ("now", "اکنون"), ("later", "بعداً"),
            ("day", "روز"), ("week", "هفته"), ("month", "ماه"), ("year", "سال"), ("hour", "ساعت"), ("minute", "دقیقه"),
            ("morning", "صبح"), ("evening", "عصر"), ("night", "شب"),
            ("money", "پول"), ("price", "قیمت"), ("free", "رایگان"), ("buy", "خرید"), ("sell", "فروش"), ("pay", "پرداخت"),
            ("work", "کار"), ("job", "شغل"), ("project", "پروژه"), ("task", "وظیفه"), ("team", "تیم"),
            ("computer", "کامپیوتر"), ("phone", "تلفن"), ("screen", "صفحه نمایش"), ("window", "پنجره"),
            ("internet", "اینترنت"), ("network", "شبکه"), ("server", "سرور"), ("data", "داده"),
            ("game", "بازی"), ("play", "بازی کردن"), ("music", "موسیقی"), ("video", "ویدیو"), ("photo", "عکس"), ("image", "تصویر"),
            ("book", "کتاب"), ("read", "خواندن"), ("write", "نوشتن"), ("text", "متن"), ("word", "کلمه"),
            ("food", "غذا"), ("water", "آب"), ("eat", "خوردن"), ("drink", "نوشیدن"),
            ("house", "خانه"), ("room", "اتاق"), ("door", "در"), ("city", "شهر"), ("country", "کشور"), ("world", "جهان"),
            ("people", "مردم"), ("person", "شخص"), ("friend", "دوست"), ("family", "خانواده"), ("child", "کودک"),
            ("man", "مرد"), ("woman", "زن"), ("boy", "پسر"), ("girl", "دختر"),
            ("love", "عشق"), ("like", "دوست داشتن"), ("want", "خواستن"), ("need", "نیاز داشتن"), ("know", "دانستن"),
            ("think", "فکر کردن"), ("see", "دیدن"), ("look", "نگاه کردن"), ("hear", "شنیدن"), ("say", "گفتن"), ("tell", "گفتن"),
            ("go", "رفتن"), ("come", "آمدن"), ("get", "گرفتن"), ("give", "دادن"), ("make", "ساختن"), ("take", "گرفتن"),
            ("help", "کمک"), ("try", "تلاش کردن"), ("use", "استفاده کردن"), ("call", "تماس"), ("ask", "پرسیدن"), ("answer", "پاسخ"),
            ("question", "سؤال"), ("problem", "مشکل"), ("solution", "راه‌حل"), ("example", "مثال"), ("information", "اطلاعات"),
            ("important", "مهم"), ("possible", "ممکن"), ("impossible", "غیرممکن"), ("different", "متفاوت"), ("same", "همان"),
            ("all", "همه"), ("some", "برخی"), ("many", "بسیاری"), ("much", "زیاد"), ("few", "کم"), ("more", "بیشتر"), ("less", "کمتر"),
            ("one", "یک"), ("two", "دو"), ("three", "سه"), ("four", "چهار"), ("five", "پنج"),
            ("six", "شش"), ("seven", "هفت"), ("eight", "هشت"), ("nine", "نه"), ("ten", "ده"),
            ("first", "اول"), ("last", "آخر"), ("here", "اینجا"), ("there", "آنجا"), ("where", "کجا"), ("when", "کی"),
            ("why", "چرا"), ("how", "چگونه"), ("what", "چه"), ("who", "چه کسی"), ("which", "کدام"),
            ("with", "با"), ("without", "بدون"), ("for", "برای"), ("from", "از"), ("to", "به"), ("in", "در"),
            ("on", "روی"), ("at", "در"), ("by", "توسط"), ("about", "درباره"), ("into", "به داخل"), ("out", "بیرون"),
            ("up", "بالا"), ("down", "پایین"), ("over", "بالای"), ("under", "زیر"), ("again", "دوباره"), ("also", "همچنین"),
            ("very", "خیلی"), ("too", "همچنین"), ("only", "فقط"), ("just", "فقط"), ("still", "هنوز"), ("already", "قبلاً"),
        };

        var table = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (en, fa) in pairs)
            table[en] = fa;

        return table;
    }
}