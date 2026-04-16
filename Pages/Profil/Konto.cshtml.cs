using BCrypt.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySql.Data.MySqlClient;
using System.ComponentModel.DataAnnotations;
using System.Data;

namespace ZamekDoDrzwi.Pages.Profil
{
    public class KontoModel : AdminPageModel
    {
        private readonly IConfiguration _config;
        private readonly ILogger<KontoModel> _logger;

        public KontoModel(IConfiguration config, ILogger<KontoModel> logger)
        {
            _config = config;
            _logger = logger;
        }

        // Bindowane formularze
        [BindProperty]
        public KontoForm Konto { get; set; } = new();

        [BindProperty]
        public HasloForm Haslo { get; set; } = new();

        [TempData]
        public string? KomunikatHaslo { get; set; }

        public string? AktywnaZakladka
        {
            get => ViewData["AktywnaZakladka"] as string;
            set => ViewData["AktywnaZakladka"] = value;
        }

        // Klasa danych konta
        public class KontoForm
        {
            public int Id { get; set; }

            [Required]
            public string Login { get; set; } = "";

            [Required, EmailAddress]
            public string Email { get; set; } = "";

            [Phone]
            public string? Telefon { get; set; }
        }

        // Klasa formularza zmiany hasła
        public class HasloForm
        {
            [Required(ErrorMessage = "Pole {0} jest wymagane.")]
            [Display(Name = "Stare hasło")]
            [DataType(DataType.Password)]
            public string StareHaslo { get; set; } = "";

            [Required(ErrorMessage = "Pole {0} jest wymagane.")]
            [Display(Name = "Nowe hasło")]
            [DataType(DataType.Password)]
            public string NoweHaslo { get; set; } = "";

            [Required(ErrorMessage = "Pole {0} jest wymagane.")]
            [Display(Name = "Potwierdź hasło")]
            [DataType(DataType.Password)]
            [Compare("NoweHaslo", ErrorMessage = "Hasła nie są takie same.")]
            public string PotwierdzHaslo { get; set; } = "";
        }

        // Załadowanie danych użytkownika
        public IActionResult OnGet()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                _logger.LogWarning("Brak sesji użytkownika przy OnGet");
                return RedirectToPage("/Index");
            }

            _logger.LogInformation("Pobieranie danych użytkownika ID: {UserId}", userId);

            try
            {
                using var db = new MySqlConnection(_config.GetConnectionString("MySql"));
                db.Open();

                string dbSql = "SELECT id, login, email, telefon FROM users WHERE id = @id";
                using var dbCmd = new MySqlCommand(dbSql, db);
                dbCmd.Parameters.AddWithValue("@id", userId.Value);

                using var dbReader = dbCmd.ExecuteReader();
                if (dbReader.Read())
                {
                    Konto.Id = dbReader.GetInt32("id");
                    Konto.Login = dbReader.GetString("login");
                    Konto.Email = dbReader.GetString("email");
                    Konto.Telefon = dbReader.IsDBNull("telefon") ? null : dbReader.GetString("telefon");

                    _logger.LogInformation("Załadowano dane: {Login}, {Email}, {Telefon}", Konto.Login, Konto.Email, Konto.Telefon);
                }
                else
                {
                    _logger.LogWarning("Nie znaleziono użytkownika ID: {UserId}", userId);
                    return RedirectToPage("/Index");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd przy pobieraniu danych użytkownika");
                KomunikatHaslo = "Wystąpił błąd podczas wczytywania danych konta.";
            }

            return Page();
        }

        // Zapis zmian danych konta
        public IActionResult OnPostZapisz()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                _logger.LogWarning("Brak sesji użytkownika przy zapisie danych");
                return RedirectToPage("/Index");
            }

            // Walidacja tylko sekcji Konto
            ModelState.Clear();
            TryValidateModel(Konto, nameof(Konto));

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Niepoprawny model przy zapisie konta");
                return Page();
            }

            try
            {
                using var db = new MySqlConnection(_config.GetConnectionString("MySql"));
                db.Open();

                string dbSql = "UPDATE users SET email = @em, telefon = @tel WHERE id = @id";
                using var dbCmd = new MySqlCommand(dbSql, db);
                dbCmd.Parameters.AddWithValue("@em", Konto.Email);
                dbCmd.Parameters.AddWithValue("@tel", string.IsNullOrWhiteSpace(Konto.Telefon) ? DBNull.Value : Konto.Telefon);
                dbCmd.Parameters.AddWithValue("@id", userId.Value);

                int affected = dbCmd.ExecuteNonQuery();
                KomunikatHaslo = affected > 0 ? "Zapisano zmiany." : "Brak zmian lub użytkownik nie istnieje.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd przy zapisie danych konta");
                ModelState.AddModelError(string.Empty, "Wystąpił błąd przy zapisie: " + ex.Message);
                return Page();
            }

            return RedirectToPage();
        }

        // Zmiana hasła użytkownika
        public IActionResult OnPostZmienHaslo()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToPage("/Index");

            ModelState.Clear();
            TryValidateModel(Haslo, nameof(Haslo));

            if (!ModelState.IsValid)
            {
                AktywnaZakladka = "haslo";
                _logger.LogWarning("Niepoprawny model przy zmianie hasła");
                return Page();
            }

            try
            {
                using var db = new MySqlConnection(_config.GetConnectionString("MySql"));
                db.Open();

                // Pobranie obecnego hasha
                string dbSql = "SELECT haslo_hash FROM users WHERE id = @id";
                using var dbCmd = new MySqlCommand(dbSql, db);
                dbCmd.Parameters.AddWithValue("@id", userId.Value);
                var hash = dbCmd.ExecuteScalar()?.ToString();

                // Weryfikacja starego hasła
                if (string.IsNullOrEmpty(hash) || !BCrypt.Net.BCrypt.Verify(Haslo.StareHaslo, hash))
                {
                    AktywnaZakladka = "haslo";
                    ModelState.AddModelError("Haslo.StareHaslo", "Nieprawidłowe obecne hasło.");
                    return Page();
                }

                // Hashowanie i aktualizacja nowego hasła
                string newHash = BCrypt.Net.BCrypt.HashPassword(Haslo.NoweHaslo);
                string dbUpdateSql = "UPDATE users SET haslo_hash = @hash WHERE id = @id";
                using var dbUpdateCmd = new MySqlCommand(dbUpdateSql, db);
                dbUpdateCmd.Parameters.AddWithValue("@hash", newHash);
                dbUpdateCmd.Parameters.AddWithValue("@id", userId.Value);

                int rows = dbUpdateCmd.ExecuteNonQuery();
                KomunikatHaslo = rows > 0 ? "Hasło zostało zmienione." : "Nie udało się zmienić hasła.";

                AktywnaZakladka = "haslo";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd przy zmianie hasła");
                ModelState.AddModelError(string.Empty, "Błąd przy zmianie hasła: " + ex.Message);
                AktywnaZakladka = "haslo";
                return Page();
            }

            return RedirectToPage();
        }
    }
}
