using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

class Program
{
    static async Task Main(string[] args)
    {
        Console.Title = "Gemini AI Tarif Önerici";
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("========================================");
        Console.WriteLine("   Gemini AI Tarif Önerici Uygulaması   ");
        Console.WriteLine("========================================\n");
        Console.ResetColor();

        // API anahtarını appsettings.json'dan oku
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var apiKey = config["GeminiApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("HATA: appsettings.json dosyasında 'GeminiApiKey' bulunamadı.");
            Console.WriteLine("Lütfen API anahtarınızı appsettings.json dosyasına ekleyin.");
            Console.ResetColor();
            return;
        }

        while (true)
        {
            // Kullanıcıdan malzemeleri al
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("Elindeki malzemeleri virgülle yaz (çıkış için 'exit'): ");
            Console.ResetColor();
            string ingredients = Console.ReadLine()?.Trim() ?? "";

            if (string.IsNullOrEmpty(ingredients) || ingredients.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\nUygulama kapatılıyor. Afiyet olsun!");
                Console.ResetColor();
                break;
            }

            // Diyet/tercih/engeller
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("Diyet/tercih/engeller (Örn: vegan, vejetaryen, glutensiz, helal, fındık alerjisi) [boş geçilebilir]: ");
            Console.ResetColor();
            string prefs = Console.ReadLine()?.Trim() ?? "";

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("\nTarif oluşturuluyor...");
            Console.ResetColor();

            try
            {
                var result = await AnalyzeRecipeAsync(ingredients, prefs, apiKey);

                // Basit post-process sezgileri
                result = ApplyPantryHeuristics(ingredients, prefs, result);

                // Başlık
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"\nTarif: {result.Title}");
                Console.ResetColor();

                // Üst bilgi
                Console.WriteLine($"Porsiyon: {result.Servings} | Zorluk: {result.Difficulty} | Mutfak: {result.Cuisine}");
                Console.WriteLine($"Hazırlık: {result.PrepTime} | Pişirme: {result.CookTime} | Toplam: {result.TotalTime}");
                if (!string.IsNullOrWhiteSpace(result.DietTags))
                    Console.WriteLine($"Etiketler: {result.DietTags}");

                // Malzemeler
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\nMalzemeler:");
                Console.ResetColor();
                foreach (var ing in result.Ingredients ?? new List<string>())
                    Console.WriteLine($" - {ing}");

                // Adımlar
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\nAdımlar:");
                Console.ResetColor();
                int stepNo = 1;
                foreach (var st in result.Steps ?? new List<string>())
                    Console.WriteLine($"{stepNo++}. {st}");

                // İpuçları
                if (result.Tips != null && result.Tips.Count > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("\nİpuçları:");
                    Console.ResetColor();
                    foreach (var tip in result.Tips)
                        Console.WriteLine($" - {tip}");
                }

                // Besin
                if (result.Nutrition != null)
                {
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.WriteLine("\nBesin Değerleri (tahmini, porsiyon başı):");
                    Console.ResetColor();
                    Console.WriteLine($" Kalori: {result.Nutrition.Calories}");
                    Console.WriteLine($" Protein: {result.Nutrition.Protein}");
                    Console.WriteLine($" Karbonhidrat: {result.Nutrition.Carbs}");
                    Console.WriteLine($" Yağ: {result.Nutrition.Fat}");
                }

                Console.WriteLine(new string('-', 60));
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Hata oluştu: {ex.Message}");
                Console.ResetColor();
                Console.WriteLine(new string('-', 60));
                Console.WriteLine();
            }
        }
    }

    // ==== SONUÇ MODELİ ====
    public class RecipeResult
    {
        public string Title { get; set; } = "Tarif";
        public string Servings { get; set; } = "2";
        public string PrepTime { get; set; } = "10 dk";
        public string CookTime { get; set; } = "20 dk";
        public string TotalTime { get; set; } = "30 dk";
        public string Difficulty { get; set; } = "Kolay";
        public string Cuisine { get; set; } = "Genel";
        public string DietTags { get; set; } = ""; // vegan/vejetaryen/glutensiz/helal vb.
        public List<string> Ingredients { get; set; } = new List<string>();
        public List<string> Steps { get; set; } = new List<string>();
        public List<string> Tips { get; set; } = new List<string>();
        public NutritionInfo Nutrition { get; set; } = new NutritionInfo();
    }

    public class NutritionInfo
    {
        public string Calories { get; set; } = "—";
        public string Protein { get; set; } = "—";
        public string Carbs { get; set; } = "—";
        public string Fat { get; set; } = "—";
    }

    // ==== API ÇAĞRISI ====
    static async Task<RecipeResult> AnalyzeRecipeAsync(string ingredientsCsv, string prefs, string apiKey)
    {
        using var httpClient = new HttpClient();

        var endpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent";
        httpClient.DefaultRequestHeaders.Add("X-goog-api-key", apiKey);

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new {
                            text =
@"Aşağıdaki **malzemelere** göre **Türkçe bir yemek tarifi** öner.
Sadece şu **JSON şemasını** döndür (başka açıklama yazma):

{
  ""title"": ""string"",
  ""servings"": ""string"",          // ör: ""2-3 kişilik""
  ""prepTime"": ""string"",          // ör: ""10 dk""
  ""cookTime"": ""string"",          // ör: ""20 dk""
  ""totalTime"": ""string"",         // ör: ""30 dk""
  ""difficulty"": ""Kolay|Orta|Zor"",
  ""cuisine"": ""string"",           // ör: ""Türk"", ""Akdeniz""
  ""dietTags"": ""string"",          // vegan, vejetaryen, glutensiz, helal vb. uygun etiketleri yaz veya boş bırak
  ""ingredients"": [""madde1"", ""madde2"", ""...""],
  ""steps"": [""adım1"", ""adım2"", ""...""],
  ""tips"": [""ipucu1"", ""ipucu2""],
  ""nutrition"": { ""calories"": ""kcal"", ""protein"": ""g"", ""carbs"": ""g"", ""fat"": ""g"" }
}

Kurallar:
- Sadece JSON yaz.
- Malzemelerde **yoksa** marketten alınması makul olan **az sayıda** ek malzemeyi (tuz, yağ, baharat gibi) ekleyebilirsin.
- Kullanıcı **diyet/tercih/engel** belirttiyse (örn: vegan, glutensiz, helal), tarifi buna **uygun** ver.
- Mümkün olduğunca **basit ve uygulanabilir** tut.
- Miktarları ve ölçüleri anlaşılır yaz (ör: ""1 yemek kaşığı"", ""200 g"", ""1 küçük soğan"").

Malzemeler:
" + ingredientsCsv + @"

Diyet/tercih/engeller:
" + (string.IsNullOrWhiteSpace(prefs) ? "(yok)" : prefs)
                        }
                    }
                }
            },
            generationConfig = new
            {
                response_mime_type = "application/json",
                temperature = 0.1,
                topP = 0.1,
                candidateCount = 1,
                maxOutputTokens = 1024
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync(endpoint, content);
        var responseString = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"API Hatası ({response.StatusCode}): {responseString}");

        // Beklenen JSON akışı
        try
        {
            using var document = JsonDocument.Parse(responseString);
            var candidates = document.RootElement.GetProperty("candidates");
            if (candidates.GetArrayLength() > 0)
            {
                var content_response = candidates[0].GetProperty("content");
                var parts = content_response.GetProperty("parts");
                if (parts.GetArrayLength() > 0)
                {
                    var raw = parts[0].GetProperty("text").GetString() ?? "";

                    // 1) Saf JSON'u ayıkla (```json ...```, metin vs.)
                    var jsonOnly = ExtractFirstJsonObject(raw);
                    if (!string.IsNullOrWhiteSpace(jsonOnly))
                    {
                        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var parsed = JsonSerializer.Deserialize<GeminiRecipeJson>(jsonOnly, options);
                        if (parsed != null && !string.IsNullOrWhiteSpace(parsed.title))
                            return MapParsed(parsed);
                    }

                    // 2) JSON yine de gelmediyse: basit fallback
                    return FallbackFromText(raw);
                }
            }
        }
        catch (JsonException)
        {
            // Beklenmedik gövde → fallback
        }

        return new RecipeResult
        {
            Title = "Uygun Tarif Bulunamadı",
            Tips = new List<string> { "Malzeme listesini daha net ve miktarlarla yazmayı deneyin." }
        };
    }

    // ==== Serbest metinden ilk geçerli {…} JSON nesnesini çıkar ====
    static string ExtractFirstJsonObject(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";

        // ````json ... ``` veya ``` ... ``` bloklarını temizle
        raw = raw.Replace("\r", "");
        if (raw.Contains("```"))
        {
            int startFence = raw.IndexOf("```", StringComparison.Ordinal);
            int endFence = raw.IndexOf("```", startFence + 3, StringComparison.Ordinal);
            if (startFence >= 0 && endFence > startFence)
            {
                var inside = raw.Substring(startFence + 3, endFence - (startFence + 3));
                if (inside.StartsWith("json", StringComparison.OrdinalIgnoreCase))
                    inside = inside.Substring(4);
                raw = inside.Trim();
            }
        }

        // İlk dengeli { … } bloğunu bul
        int start = raw.IndexOf('{');
        if (start < 0) return "";

        int depth = 0;
        for (int i = start; i < raw.Length; i++)
        {
            if (raw[i] == '{') depth++;
            else if (raw[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return raw.Substring(start, (i - start) + 1).Trim();
                }
            }
        }
        return "";
    }

    // ==== MODEL JSON DTO ====
    private class GeminiRecipeJson
    {
        public string title { get; set; }
        public string servings { get; set; }
        public string prepTime { get; set; }
        public string cookTime { get; set; }
        public string totalTime { get; set; }
        public string difficulty { get; set; }
        public string cuisine { get; set; }
        public string dietTags { get; set; }
        public List<string> ingredients { get; set; }
        public List<string> steps { get; set; }
        public List<string> tips { get; set; }
        public NutritionJson nutrition { get; set; }
    }

    private class NutritionJson
    {
        public string calories { get; set; }
        public string protein { get; set; }
        public string carbs { get; set; }
        public string fat { get; set; }
    }

    private static RecipeResult MapParsed(GeminiRecipeJson p)
    {
        return new RecipeResult
        {
            Title = p.title ?? "Tarif",
            Servings = p.servings ?? "2",
            PrepTime = p.prepTime ?? "—",
            CookTime = p.cookTime ?? "—",
            TotalTime = p.totalTime ?? "—",
            Difficulty = p.difficulty ?? "Kolay",
            Cuisine = p.cuisine ?? "Genel",
            DietTags = p.dietTags ?? "",
            Ingredients = p.ingredients ?? new List<string>(),
            Steps = p.steps ?? new List<string>(),
            Tips = p.tips ?? new List<string>(),
            Nutrition = new NutritionInfo
            {
                Calories = p.nutrition?.calories ?? "—",
                Protein = p.nutrition?.protein ?? "—",
                Carbs = p.nutrition?.carbs ?? "—",
                Fat = p.nutrition?.fat ?? "—"
            }
        };
    }

    // ==== Fallback: Serbest metinden sade tarif kur (çok nadir gerekebilir) ====
    private static RecipeResult FallbackFromText(string text)
    {
        var rr = new RecipeResult
        {
            Title = "Pratik Tava Tarifi",
            Servings = "2",
            PrepTime = "10 dk",
            CookTime = "15 dk",
            TotalTime = "25 dk",
            Difficulty = "Kolay",
            Cuisine = "Genel",
            Ingredients = new List<string>
            {
                "2 yemek kaşığı zeytinyağı",
                "Tuz, karabiber",
                "Seçtiğiniz sebzeler"
            },
            Steps = new List<string>
            {
                "Tavayı ısıtın ve yağı ekleyin.",
                "Sebzeleri ekleyip soteleyin.",
                "Tuz ve karabiberle tatlandırıp servis edin."
            },
            Tips = new List<string> { "Baharatları damak tadınıza göre artırabilirsiniz." }
        };
        return rr;
    }

    // ==== POST-PROCESS / HEURISTICS ====
    static RecipeResult ApplyPantryHeuristics(string ingredientsCsv, string prefs, RecipeResult r)
    {
        var s = (ingredientsCsv ?? "").ToLower();
        var p = (prefs ?? "").ToLower();

        bool vegan = p.Contains("vegan");
        bool veg = p.Contains("vejetaryen") || p.Contains("vegetarian");
        bool helal = p.Contains("helal");
        bool glutensiz = p.Contains("glutensiz") || p.Contains("gluten-free");

        // Etiketleri genişlet
        var tags = new List<string>();
        if (!string.IsNullOrWhiteSpace(r.DietTags))
            tags.AddRange(r.DietTags.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()));

        if (vegan && !tags.Any(t => t.Equals("vegan", StringComparison.OrdinalIgnoreCase))) tags.Add("vegan");
        if (veg && !tags.Any(t => t.Equals("vejetaryen", StringComparison.OrdinalIgnoreCase))) tags.Add("vejetaryen");
        if (helal && !tags.Any(t => t.Equals("helal", StringComparison.OrdinalIgnoreCase))) tags.Add("helal");
        if (glutensiz && !tags.Any(t => t.Equals("glutensiz", StringComparison.OrdinalIgnoreCase))) tags.Add("glutensiz");

        r.DietTags = string.Join(", ", tags);

        // Basit ikame önerileri (ipuçlarına ekler)
        var tips = r.Tips ?? new List<string>();
        if (vegan)
        {
            tips.Add("Vegan uyum için süt ürünlerini bitkisel muadilleriyle değiştirin (badem sütü, hindistan cevizi sütü vb.).");
            tips.Add("Bal yerine akçaağaç şurubu veya hurma özü kullanabilirsiniz.");
        }
        if (glutensiz)
        {
            tips.Add("Glutensiz makarna/ekmek veya pirinç-buckwheat gibi tahılları tercih edin.");
            tips.Add("Soslarda un yerine mısır nişastası/patates nişastası kullanın.");
        }
        if (helal)
        {
            tips.Add("Et/işlenmiş ürünlerin helal sertifikalı olmasına dikkat edin.");
        }

        // Elinde temel malzeme yoksa öneri
        bool hasOil = s.Contains("zeytinyağı") || s.Contains("ayçiçek") || s.Contains("tereyağı") || s.Contains("yağ");
        if (!hasOil && !(r.Ingredients?.Any(x => x.ToLower().Contains("yağ")) ?? false))
            tips.Add("Yağ yoksa az miktarda su ile soteleyip en sonda 1-2 yemek kaşığı yağ ekleyebilirsiniz.");

        r.Tips = tips.Distinct().ToList();
        return r;
    }
}
