# Data Migration & Validation

Bozuk, eksik ve mükerrer müşteri/sipariş kayıtlarını temizleyerek yeni SQL Server şemasına güvenli biçimde taşıyan .NET uygulamasıdır.

## Özellikler

- Telefon, e-posta, kimlik, tutar, tarih ve yabancı anahtar validasyonu
- Metin, telefon, e-posta ve kimlik numarası temizleme
- TC kimlik veya e-posta + ad soyad kuralıyla deduplication
- Kurtarılamayan kayıtlar için `Failed_Records` karantina tablosu
- Transaction, hata simülasyonu ve rollback
- Kaynak, hedef ve karantina kayıtları için mutabakat kontrolü
- Keyset pagination kullanan yapılandırılabilir batch/chunk akışı
- Sınırlı paralellik kullanan migration worker havuzu
- 100.000 kayıtlık otomatik SQL Server yük ve rollback testi

## Gereksinimler

- .NET 10 SDK
- SQL Server Express (`.\SQLEXPRESS`)
- İsteğe bağlı olarak `make`

Uygulama örnek kaynak ve hedef veritabanlarını `EnsureCreated` ile oluşturur.

## Çalıştırma

```powershell
make run
```

veya:

```powershell
dotnet run --project DataMigrationValidation.Console -- --batch-size=500 --workers=4
```

## Rollback testi

```powershell
make rollback
```

Beklenen durum:

```text
Status: RolledBack
Error: Simulated critical migration failure.
```

## Büyük veri testi

```powershell
make load-test
```

Test 20.000 müşteri ve 80.000 sipariş üretir; normal migration, mutabakat ve rollback sonuçlarını doğrular. Benzersiz adlarla oluşturduğu geçici test veritabanlarını tamamlandığında otomatik olarak siler.

## Proje yapısı

- `DataMigrationValidation.Core`: Entity, temizleme, validasyon, deduplication ve rapor modelleri
- `DataMigrationValidation.Infrastructure`: EF Core context'leri, migration pipeline ve worker altyapısı
- `DataMigrationValidation.Console`: Uygulama giriş noktası
- `DataMigrationValidation.LoadTests`: Büyük veri entegrasyon/yük testi
