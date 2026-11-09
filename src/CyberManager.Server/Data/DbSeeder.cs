using System.Security.Cryptography;
using CyberManager.Server.Security;
using CyberManager.Server.Services;
using CyberManager.Shared;
using CyberManager.Shared.Domain;
using CyberManager.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CyberManager.Server.Data;

public static class DbSeeder
{
    /// <summary>
    /// <c>EnsureCreated</c> no agrega tablas ni columnas a una base ya existente. Este script
    /// mantiene compatibles las instalaciones creadas antes de las funciones nuevas.
    /// </summary>
    public static async Task EnsureCompatibilityAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "Wallpapers" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Wallpapers" PRIMARY KEY AUTOINCREMENT,
                "Name" TEXT NOT NULL,
                "ContentType" TEXT NOT NULL,
                "Extension" TEXT NOT NULL,
                "Data" BLOB NOT NULL,
                "Width" INTEGER NOT NULL,
                "Height" INTEGER NOT NULL,
                "SizeBytes" INTEGER NOT NULL,
                "Sha256" TEXT NOT NULL,
                "Active" INTEGER NOT NULL,
                "SortOrder" INTEGER NOT NULL,
                "CreatedUtc" TEXT NOT NULL,
                "UpdatedUtc" TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_Wallpapers_SortOrder" ON "Wallpapers" ("SortOrder");

            CREATE TABLE IF NOT EXISTS "PromoVideos" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_PromoVideos" PRIMARY KEY AUTOINCREMENT,
                "Name" TEXT NOT NULL,
                "ContentType" TEXT NOT NULL,
                "Extension" TEXT NOT NULL,
                "Data" BLOB NOT NULL,
                "SizeBytes" INTEGER NOT NULL,
                "Sha256" TEXT NOT NULL,
                "Active" INTEGER NOT NULL,
                "SortOrder" INTEGER NOT NULL,
                "CreatedUtc" TEXT NOT NULL,
                "UpdatedUtc" TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_PromoVideos_SortOrder" ON "PromoVideos" ("SortOrder");

            CREATE TABLE IF NOT EXISTS "SupplierPayments" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_SupplierPayments" PRIMARY KEY AUTOINCREMENT,
                "SupplierId" INTEGER NOT NULL,
                "DateUtc" TEXT NOT NULL,
                "Amount" TEXT NOT NULL,
                "Method" INTEGER NOT NULL,
                "Reference" TEXT NULL,
                "Notes" TEXT NULL,
                "UserId" INTEGER NULL,
                CONSTRAINT "FK_SupplierPayments_Suppliers_SupplierId"
                    FOREIGN KEY ("SupplierId") REFERENCES "Suppliers" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS "IX_SupplierPayments_SupplierId" ON "SupplierPayments" ("SupplierId");
            CREATE INDEX IF NOT EXISTS "IX_SupplierPayments_DateUtc" ON "SupplierPayments" ("DateUtc");

            CREATE TABLE IF NOT EXISTS "WebFilterRules" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_WebFilterRules" PRIMARY KEY AUTOINCREMENT,
                "Pattern" TEXT NOT NULL,
                "Kind" INTEGER NOT NULL,
                "Active" INTEGER NOT NULL,
                "Notes" TEXT NULL,
                "CreatedUtc" TEXT NOT NULL,
                "UpdatedUtc" TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_WebFilterRules_Pattern" ON "WebFilterRules" ("Pattern");
            CREATE INDEX IF NOT EXISTS "IX_WebFilterRules_Kind" ON "WebFilterRules" ("Kind");
            """);

        // Columnas añadidas para el control de consolas de videojuegos.
        await AddColumnIfMissingAsync(db, "Stations", "Kind", "INTEGER NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync(db, "Stations", "Platform", "TEXT NULL");
        await AddColumnIfMissingAsync(db, "Stations", "Controllers", "INTEGER NULL");
    }

    private static async Task AddColumnIfMissingAsync(AppDbContext db, string table, string column, string definition)
    {
        var connection = db.Database.GetDbConnection();
        var wasClosed = connection.State != System.Data.ConnectionState.Open;
        if (wasClosed) await connection.OpenAsync();

        var exists = false;
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info(\"{table}\");";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }
        }
        finally
        {
            if (wasClosed) await connection.CloseAsync();
        }

        if (!exists)
        {
            // Los nombres de tabla/columna y la definición provienen de constantes internas
            // del sembrador, no de datos de usuario; se suprimen por eso la regla de EF.
#pragma warning disable EF1002
            await db.Database.ExecuteSqlRawAsync(
                $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {definition};");
#pragma warning restore EF1002
        }
    }

    public static async Task SeedAsync(AppDbContext db)
    {
        await SeedUsersAsync(db);
        await SeedSettingsAsync(db);
        await SeedCatalogAsync(db);
        await SeedStationsAsync(db);
        await SeedRateAsync(db);
        await db.SaveChangesAsync();
        await SeedWallpapersAsync(db);
        await SeedWebFilterAsync(db);
    }

    /// <summary>Versión del paquete de fondos incluido; al cambiarla se vuelve a sembrar.</summary>
    private const string BundledWallpapersVersion = "1";

    private static readonly IReadOnlyDictionary<string, string> BundledWallpaperNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["01-league-of-legends"] = "League of Legends",
            ["02-free-fire"] = "Free Fire",
            ["03-valorant"] = "Valorant",
            ["04-counter-strike-2"] = "Counter-Strike 2",
            ["05-gta-v"] = "GTA V",
            ["06-gta-vi"] = "GTA VI",
            ["07-minecraft"] = "Minecraft",
            ["08-roblox"] = "Roblox",
            ["09-fortnite"] = "Fortnite",
            ["10-call-of-duty"] = "Call of Duty",
            ["11-apex-legends"] = "Apex Legends",
            ["12-dota-2"] = "Dota 2",
            ["13-pubg"] = "PUBG",
            ["14-among-us"] = "Among Us",
            ["15-elden-ring"] = "Elden Ring",
            ["16-cyberpunk-2077"] = "Cyberpunk 2077",
            ["17-ea-sports-fc"] = "EA Sports FC",
            ["18-efootball"] = "eFootball",
            ["19-genshin-impact"] = "Genshin Impact",
            ["20-mobile-legends"] = "Mobile Legends",
            ["21-clash-royale"] = "Clash Royale",
            ["22-brawl-stars"] = "Brawl Stars",
            ["23-rocket-league"] = "Rocket League",
            ["24-rainbow-six-siege"] = "Rainbow Six Siege",
            ["25-overwatch-2"] = "Overwatch 2",
            ["26-red-dead-redemption-2"] = "Red Dead Redemption 2",
            ["27-god-of-war"] = "God of War",
            ["28-spider-man-2"] = "Spider-Man 2",
            ["29-dragon-ball"] = "Dragon Ball",
            ["30-mortal-kombat-1"] = "Mortal Kombat 1"
        };

    /// <summary>
    /// Copia a la galería los fondos 4K que vienen con el instalador. Solo se ejecuta una vez
    /// por versión de paquete; si el usuario borra un fondo, no se vuelve a agregar.
    /// </summary>
    private static async Task SeedWallpapersAsync(AppDbContext db)
    {
        var version = await db.Settings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == SettingsKeys.BundledWallpapersVersion);
        if (string.Equals(version?.Value, BundledWallpapersVersion, StringComparison.Ordinal))
            return;

        var folder = Path.Combine(AppContext.BaseDirectory, "Assets", "Wallpapers");
        if (Directory.Exists(folder))
        {
            var existing = await db.Wallpapers.AsNoTracking()
                .Select(w => w.Sha256)
                .ToListAsync();
            var known = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
            var order = await db.Wallpapers.MaxAsync(w => (int?)w.SortOrder) ?? 0;

            var files = Directory.EnumerateFiles(folder, "*.jpg")
                .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase);

            foreach (var file in files)
            {
                byte[] data;
                try
                {
                    data = await File.ReadAllBytesAsync(file);
                }
                catch (IOException)
                {
                    continue;
                }

                var probe = ImageInspector.Detect(data);
                if (!probe.Valid) continue;

                var hash = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
                if (!known.Add(hash)) continue;

                var key = Path.GetFileNameWithoutExtension(file);
                var name = BundledWallpaperNames.TryGetValue(key, out var pretty) ? pretty : key;
                order++;

                db.Wallpapers.Add(new Wallpaper
                {
                    Name = name,
                    ContentType = probe.ContentType,
                    Extension = probe.Extension,
                    Data = data,
                    Width = probe.Width,
                    Height = probe.Height,
                    SizeBytes = data.LongLength,
                    Sha256 = hash,
                    Active = true,
                    SortOrder = order,
                    CreatedUtc = DateTime.UtcNow,
                    UpdatedUtc = DateTime.UtcNow
                });
            }
        }

        await db.SaveChangesAsync();

        var setting = await db.Settings.FirstOrDefaultAsync(x => x.Key == SettingsKeys.BundledWallpapersVersion);
        if (setting is null)
            db.Settings.Add(new Setting { Key = SettingsKeys.BundledWallpapersVersion, Value = BundledWallpapersVersion });
        else
            setting.Value = BundledWallpapersVersion;

        await db.SaveChangesAsync();
    }

    /// <summary>Versión de la lista inicial de NetFiltroWeb.</summary>
    private const string BundledWebFilterVersion = "1";

    public static readonly (string Pattern, WebFilterRuleKind Kind, string Notes)[] DefaultWebFilterRules =
    {
        ("pornhub.com", WebFilterRuleKind.Domain, "Sitio para adultos"),
        ("xvideos.com", WebFilterRuleKind.Domain, "Sitio para adultos"),
        ("xnxx.com", WebFilterRuleKind.Domain, "Sitio para adultos"),
        ("xhamster.com", WebFilterRuleKind.Domain, "Sitio para adultos"),
        ("redtube.com", WebFilterRuleKind.Domain, "Sitio para adultos"),
        ("youporn.com", WebFilterRuleKind.Domain, "Sitio para adultos"),
        ("tube8.com", WebFilterRuleKind.Domain, "Sitio para adultos"),
        ("spankbang.com", WebFilterRuleKind.Domain, "Sitio para adultos"),
        ("onlyfans.com", WebFilterRuleKind.Domain, "Contenido para adultos"),
        ("chaturbate.com", WebFilterRuleKind.Domain, "Cámaras para adultos"),
        ("livejasmin.com", WebFilterRuleKind.Domain, "Cámaras para adultos"),
        ("bongacams.com", WebFilterRuleKind.Domain, "Cámaras para adultos"),
        ("stripchat.com", WebFilterRuleKind.Domain, "Cámaras para adultos"),
        ("cam4.com", WebFilterRuleKind.Domain, "Cámaras para adultos"),
        ("myfreecams.com", WebFilterRuleKind.Domain, "Cámaras para adultos"),
        ("literotica.com", WebFilterRuleKind.Domain, "Contenido para adultos"),
        ("adultfriendfinder.com", WebFilterRuleKind.Domain, "Contenido para adultos"),
        ("porn", WebFilterRuleKind.Keyword, "Palabra prohibida"),
        ("xxx", WebFilterRuleKind.Keyword, "Palabra prohibida"),
        ("hentai", WebFilterRuleKind.Keyword, "Palabra prohibida"),
        ("camsex", WebFilterRuleKind.Keyword, "Palabra prohibida"),
        ("sexcam", WebFilterRuleKind.Keyword, "Palabra prohibida"),
        ("escort", WebFilterRuleKind.Keyword, "Palabra prohibida"),
        ("onlyfans", WebFilterRuleKind.Keyword, "Palabra prohibida")
    };

    /// <summary>
    /// Siembra la lista inicial de NetFiltroWeb una sola vez por versión. Si el
    /// administrador elimina una regla, no se vuelve a agregar automáticamente.
    /// </summary>
    private static async Task SeedWebFilterAsync(AppDbContext db)
    {
        var version = await db.Settings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == SettingsKeys.WebFilterSeedVersion);
        if (string.Equals(version?.Value, BundledWebFilterVersion, StringComparison.Ordinal))
            return;

        var existing = await db.WebFilterRules.AsNoTracking()
            .Select(r => r.Pattern)
            .ToListAsync();
        var known = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;

        foreach (var (pattern, kind, notes) in DefaultWebFilterRules)
        {
            if (!known.Add(pattern)) continue;
            db.WebFilterRules.Add(new WebFilterRule
            {
                Pattern = pattern,
                Kind = kind,
                Active = true,
                Notes = notes,
                CreatedUtc = now,
                UpdatedUtc = now
            });
        }

        await db.SaveChangesAsync();

        var setting = await db.Settings.FirstOrDefaultAsync(x => x.Key == SettingsKeys.WebFilterSeedVersion);
        if (setting is null)
            db.Settings.Add(new Setting
            {
                Key = SettingsKeys.WebFilterSeedVersion,
                Value = BundledWebFilterVersion
            });
        else
            setting.Value = BundledWebFilterVersion;

        await db.SaveChangesAsync();
    }

    private static async Task SeedUsersAsync(AppDbContext db)    {
        if (await db.Users.AnyAsync()) return;

        var (adminHash, adminSalt) = PasswordHasher.Create("admin123");
        var (cashHash, cashSalt) = PasswordHasher.Create("cajero123");

        db.Users.AddRange(
            new UserAccount
            {
                Username = "admin", FullName = "Administrador", Role = UserRole.Admin,
                PasswordHash = adminHash, PasswordSalt = adminSalt
            },
            new UserAccount
            {
                Username = "cajero", FullName = "Cajero", Role = UserRole.Cashier,
                PasswordHash = cashHash, PasswordSalt = cashSalt
            });
    }

    private static async Task SeedSettingsAsync(AppDbContext db)
    {
        var defaults = new Dictionary<string, string>
        {
            [SettingsKeys.BusinessName] = "CyberManager",
            [SettingsKeys.BusinessTaxId] = "J-00000000-0",
            [SettingsKeys.BusinessAddress] = "Av. Principal, Local 1",
            [SettingsKeys.BusinessPhone] = "000-0000000",
            [SettingsKeys.CurrencySymbol] = "$",
            [SettingsKeys.LocalCurrencySymbol] = "Bs",
            [SettingsKeys.DefaultTaxRate] = "16",
            [SettingsKeys.IgtfEnabled] = "true",
            [SettingsKeys.IgtfRate] = "3",
            [SettingsKeys.TicketHeader] = "CyberManager",
            [SettingsKeys.TicketFooter] = "¡Gracias por su visita!",
            [SettingsKeys.AutoPrintTicket] = "false",
            [SettingsKeys.TicketCopies] = "1",
            [SettingsKeys.ScannerAutoAdd] = "true",
            [SettingsKeys.WarningMinutes] = "5",
            [SettingsKeys.GraceMinutes] = "2",
            [SettingsKeys.AutoLockOvertime] = "true",
            [SettingsKeys.AllowPostpaid] = "true",
            [SettingsKeys.PrinterName] = "",
            [SettingsKeys.KioskMode] = "false",
            [SettingsKeys.InvoicePrefix] = "00",
            [SettingsKeys.BackupAutoEnabled] = "true",
            [SettingsKeys.BackupIntervalHours] = "24",
            [SettingsKeys.BackupRetention] = "30",
            [SettingsKeys.BcvAutoUpdate] = "true",
            [SettingsKeys.UpdateReminderEnabled] = "true",
            [SettingsKeys.UpdateReminderDelayHours] = "2",
            [SettingsKeys.UpdateReminderDismissedVersion] = string.Empty,
            [SettingsKeys.WallpaperMode] = nameof(WallpaperMode.None),
            [SettingsKeys.WallpaperRotationSeconds] = WallpaperSettings.Invariant(
                WallpaperSettings.DefaultRotationSeconds),
            [SettingsKeys.WallpaperActiveId] = string.Empty,
            [SettingsKeys.BundledWallpapersVersion] = string.Empty,
            [SettingsKeys.SessionBarDescription] = "Sesión activa · consulte su tiempo y consumo",
            [SettingsKeys.SessionBarScale] = "100",
            [SettingsKeys.VncViewerPath] = string.Empty,
            [SettingsKeys.VncDefaultPort] = "5900",
            [SettingsKeys.WebFilterSeedVersion] = string.Empty,
            [SettingsKeys.WebFilterProxyEnabled] = "true",
            [SettingsKeys.WebFilterProxyPort] = "8877"
        };

        // Agrega solo las claves nuevas, sin sobrescribir la configuración existente.
        var existing = await db.Settings.AsNoTracking().Select(x => x.Key).ToListAsync();
        var missing = defaults
            .Where(kv => !existing.Contains(kv.Key, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (missing.Count == 0) return;

        db.Settings.AddRange(missing.Select(kv => new Setting
        {
            Key = kv.Key, Value = kv.Value
        }));
    }

    private static async Task SeedCatalogAsync(AppDbContext db)
    {
        if (!await db.Zones.AnyAsync())
        {
            db.Zones.AddRange(
                new Zone { Name = "Estándar", Color = "#2563EB", SortOrder = 1 },
                new Zone { Name = "VIP", Color = "#7C3AED", SortOrder = 2 },
                new Zone { Name = "Consolas", Color = "#059669", SortOrder = 3 });
        }

        if (!await db.Rates.AnyAsync())
        {
            db.Rates.AddRange(
                new Rate { Name = "Estándar", PricePerHour = 1.00m, MinimumMinutes = 1, BillingBlockMinutes = 1 },
                new Rate { Name = "VIP", PricePerHour = 1.50m, MinimumMinutes = 1, BillingBlockMinutes = 1 },
                new Rate { Name = "Consolas", PricePerHour = 2.00m, MinimumMinutes = 1, BillingBlockMinutes = 1 });
        }

        if (!await db.TimePackages.AnyAsync())
        {
            db.TimePackages.AddRange(
                new TimePackage { Name = "30 minutos", Minutes = 30, Price = 0.50m },
                new TimePackage { Name = "1 hora", Minutes = 60, Price = 1.00m },
                new TimePackage { Name = "2 horas", Minutes = 120, Price = 1.80m, BonusMinutes = 10 },
                new TimePackage { Name = "3 horas", Minutes = 180, Price = 2.50m, BonusMinutes = 30 },
                new TimePackage { Name = "Noche gamer (5h)", Minutes = 300, Price = 4.00m, BonusMinutes = 60 });
        }

        if (!await db.Categories.AnyAsync())
        {
            db.Categories.AddRange(
                new Category { Name = "Bebidas", Color = "#0EA5E9", SortOrder = 1 },
                new Category { Name = "Snacks", Color = "#F59E0B", SortOrder = 2 },
                new Category { Name = "Comida", Color = "#EF4444", SortOrder = 3 },
                new Category { Name = "Servicios", Color = "#10B981", SortOrder = 4 });
        }

        await db.SaveChangesAsync();

        if (!await db.Products.AnyAsync())
        {
            var cats = await db.Categories.ToDictionaryAsync(c => c.Name, c => c.Id);
            db.Products.AddRange(
                Prod("BEB-001", "Refresco 350ml", cats["Bebidas"], 1.00m, 0.60m, 24, 6),
                Prod("BEB-002", "Agua mineral 500ml", cats["Bebidas"], 0.50m, 0.25m, 30, 10),
                Prod("BEB-003", "Café pequeño", cats["Bebidas"], 0.75m, 0.30m, 50, 10),
                Prod("BEB-004", "Chocolate caliente", cats["Bebidas"], 1.20m, 0.50m, 40, 10),
                Prod("BEB-005", "Jugo natural", cats["Bebidas"], 1.50m, 0.70m, 20, 5),
                Prod("SNK-001", "Papas fritas", cats["Snacks"], 1.00m, 0.55m, 25, 6),
                Prod("SNK-002", "Galletas", cats["Snacks"], 0.80m, 0.40m, 30, 8),
                Prod("SNK-003", "Chicle", cats["Snacks"], 0.30m, 0.10m, 60, 15),
                Prod("SNK-004", "Maní", cats["Snacks"], 0.90m, 0.45m, 20, 5),
                Prod("COM-001", "Empanada", cats["Comida"], 1.50m, 0.80m, 15, 5),
                Prod("COM-002", "Perro caliente", cats["Comida"], 1.80m, 0.90m, 15, 5),
                Prod("COM-003", "Sándwich", cats["Comida"], 2.00m, 1.10m, 10, 3),
                Prod("SRV-001", "Impresión B/N (hoja)", cats["Servicios"], 0.10m, 0.02m, 0, 0, true),
                Prod("SRV-002", "Impresión color (hoja)", cats["Servicios"], 0.25m, 0.05m, 0, 0, true),
                Prod("SRV-003", "Escaneo (hoja)", cats["Servicios"], 0.15m, 0.00m, 0, 0, true),
                Prod("SRV-004", "Fotocopia", cats["Servicios"], 0.05m, 0.01m, 0, 0, true));
        }

        if (!await db.InvoiceSequences.AnyAsync())
        {
            db.InvoiceSequences.AddRange(
                new InvoiceSequence { Type = InvoiceType.Factura, Prefix = "00", NextNumber = 1, LastNumber = 99999999 },
                new InvoiceSequence { Type = InvoiceType.NotaEntrega, Prefix = "NE", NextNumber = 1, LastNumber = 99999999 });
        }
    }

    private static Product Prod(string sku, string name, int categoryId, decimal price, decimal cost,
        decimal stock, decimal minStock, bool service = false) => new()
    {
        Sku = sku, Name = name, CategoryId = categoryId, Price = price, Cost = cost,
        Stock = stock, MinStock = minStock, Unit = "pza", IsService = service, TaxRate = 16m
    };

    private static async Task SeedStationsAsync(AppDbContext db)
    {
        if (await db.Stations.AnyAsync()) return;

        var zone = await db.Zones.FirstAsync(z => z.Name == "Estándar");
        var rate = await db.Rates.FirstAsync(r => r.Name == "Estándar");

        for (var i = 1; i <= 8; i++)
        {
            db.Stations.Add(new Station
            {
                Name = $"PC-{i:D2}",
                ZoneId = zone.Id,
                RateId = rate.Id,
                SortOrder = i,
                Kind = StationKind.Pc,
                Status = StationStatus.Offline,
                IsLocked = true,
                DeviceToken = Guid.NewGuid().ToString("N")
            });
        }

        var consoleZone = await db.Zones.FirstAsync(z => z.Name == "Consolas");
        var consoleRate = await db.Rates.FirstAsync(r => r.Name == "Consolas");

        db.Stations.AddRange(
            new Station
            {
                Name = "CON-01",
                ZoneId = consoleZone.Id,
                RateId = consoleRate.Id,
                SortOrder = 9,
                Kind = StationKind.Console,
                Platform = "PlayStation 5",
                Controllers = 4,
                Status = StationStatus.Offline,
                IsLocked = true,
                DeviceToken = Guid.NewGuid().ToString("N")
            },
            new Station
            {
                Name = "CON-02",
                ZoneId = consoleZone.Id,
                RateId = consoleRate.Id,
                SortOrder = 10,
                Kind = StationKind.Console,
                Platform = "Xbox Series X",
                Controllers = 4,
                Status = StationStatus.Offline,
                IsLocked = true,
                DeviceToken = Guid.NewGuid().ToString("N")
            });
    }

    private static async Task SeedRateAsync(AppDbContext db)
    {
        if (await db.ExchangeRates.AnyAsync()) return;

        db.ExchangeRates.Add(new ExchangeRate
        {
            Date = DateTime.Today,
            Rate = 40.00m,
            Source = RateSource.Manual,
            Notes = "Tasa de ejemplo. Actualícela con la tasa del día."
        });
    }
}
