# 🍲 NetCoreAI.YemekTarifOnerici

Bu proje, **C# (.NET 8)** kullanılarak geliştirilmiş bir **yemek tarif önerici uygulamasıdır**.  
Kullanıcıdan alınan malzemeler ve tercihlere göre **Google Gemini API** üzerinden pratik tarifler oluşturur.  

## 🚀 Özellikler
- Konsol tabanlı kullanıcı arayüzü  
- `appsettings.json` dosyasından **Gemini API anahtarını** okur  
- Kullanıcıdan şu bilgileri alır:  
  - Elindeki malzemeler  
  - Diyet/tercih/engeller (örn: vegan, glutensiz, helal, alerji vb.)  
- Çıktı:  
  - Tarif başlığı  
  - Porsiyon, hazırlık/pişirme/total süre  
  - Zorluk seviyesi ve mutfak türü  
  - Diyet etiketleri  
  - Malzemeler listesi  
  - Adım adım yapılış  
  - İpuçları  
  - Besin değerleri (kalori, protein, karbonhidrat, yağ)  

## 📂 Proje Yapısı
- **Program.cs** → Ana uygulama, kullanıcı etkileşimi ve akış  
- **appsettings.json** → API anahtarı konfigürasyonu  
- **RecipeResult** → Tarif modeli (başlık, porsiyon, süreler, malzeme, adımlar vb.)  
- **AnalyzeRecipeAsync** → Gemini API çağrısı yapan fonksiyon  
- **ApplyPantryHeuristics** → Diyet/tercihlere göre ek ipuçları üretir  

## ⚙️ Kurulum
1. Bu projeyi klonlayın veya indirin:  
   ```bash

## 2. appsettings.json dosyasını oluşturun ve kendi Gemini API anahtarınızı ekleyin:
{
  "GeminiApiKey": "YOUR_API_KEY_HERE"
}
## 3.Uygulamayı çalıştırın: dotnet run

## 🖥️ Kullanım

Program çalıştığında sizden sırasıyla şunları ister:

Elinizdeki malzemeler → ör: makarna, domates, soğan, zeytinyağı

Diyet/tercih/engeller → ör: vegan, glutensiz, helal, fındık alerjisi

Program bu girdilere uygun bir tarif üretir ve konsolda detaylı olarak gösterir.

## 📊 Örnek Çalışma
<img width="1917" height="1130" alt="image" src="https://github.com/user-attachments/assets/9db0ea2f-2a8e-4c89-9035-99a70a919a76" />

## ✍️ Not: bin/ ve obj/ klasörleri .gitignore ile dışlanmıştır.

---

👉 İstersen ben bunu senin için **hazır `README.md` dosyası olarak oluşturup** projene eklenebilir hale getireyim mi?


   git clone https://github.com/<kullanici-adiniz>/NetCoreAI.YemekTarifOnerici.git
   cd NetCoreAI.YemekTarifOnerici
