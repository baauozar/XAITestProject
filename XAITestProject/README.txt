Tam paket: .NET 8 API + Flask (dinamik)

Çalıştırma
1) Flask
   - klasör: nlp
   - python -m venv .venv  &&  .venv\Scripts\activate  (macOS/Linux: source .venv/bin/activate)
   - pip install -r requirements.txt
   - python PythonApplication1.py   (http://127.0.0.1:8001/health)

2) .NET API
   - klasör: src/CvScoring.Api
   - dotnet restore
   - dotnet run     (http://localhost:5000/swagger)

Akış
- API önce Flask /score ve /extract çağırır.
- Flask çalışmıyorsa API TF‑IDF + kural motoru ile lokal hesaplar, DOCX/PDF'i yerel okur.

Uçlar
- POST /api/Scoring/score         body: { cv_text, job_text }
- POST /api/Scoring/score-file    form-data: cv_file, job_file veya job_text
- GET  /api/health

Örnekler
- examples klasöründe test dosyaları var.

Notlar
- appsettings.json → Flask.BaseUrl
- Boyut sınırı: 25 MB
- Açıklama alanı: explanation (liste) ve explanationText (tek paragraf)
- Dinamik gereksinim çıkarma: RequirementExtractor
- Kural ayarları: RuleEngine

Oluşturma tarihi (UTC): 2025-10-18T12:29:22.125211Z
