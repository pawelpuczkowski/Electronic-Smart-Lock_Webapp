using BCrypt.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySql.Data.MySqlClient;
using System.ComponentModel.DataAnnotations;
using System.Data;

namespace ZamekDoDrzwi.Pages.Uzytkownik
{
    // Model strony "Mój profil / Konto" w panelu użytkownika
    public class KontoModel : UserPageModel
    {
        private readonly IConfiguration _config;
        private readonly ILogger<KontoModel> _logger;

        public KontoModel(IConfiguration config, ILogger<KontoModel> logger)
        {
            _config = config;
            _logger = logger;
        }

        // Formularz danych konta (email, telefon)
        [BindProperty]
        public KontoForm Konto { get; set; } = new();

        // Formularz zmiany hasła
        [BindProperty]
        public HasloForm Haslo { get; set; } = new();

        // Komunikaty zwrotne po operacjach (przechowywane tymczasowo)
        [TempData]
        public string? Komunikat { get; set; }

        // Aktywna zakładka (ustawiana po walidacji błędu)
        public string? AktywnaZakladka
        {
            get => ViewData["AktywnaZakladka"] as string;
            set => ViewData["AktywnaZakladka"] = value;
        }
       
        public class KontoForm
        {
            public int Id { get; set; }

            [Required]
            public string Login { get; set; } = "";

            [Required]
            [EmailAddress]
            public string Email { get; set; } = "";

            [Phone]
            public string? Telefon { get; set; }
        }

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
        
        // Wczytanie danych profilu     
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

                string sql = "SELECT id, login, email, telefon FROM users WHERE id = @id";
                using var cmd = new MySqlCommand(sql, db);
                cmd.Parameters.AddWithValue("@id", userId.Value);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    Konto.Id = reader.GetInt32("id");
                    Konto.Login = reader.GetString("login");
                    Konto.Email = reader.GetString("email");
                    Konto.Telefon = reader.IsDBNull("telefon") ? null : reader.GetString("telefon");

                    _logger.LogInformation("Załadowano dane użytkownika: {Login}, {Email}, {Telefon}",
                        Konto.Login, Konto.Email, Konto.Telefon);
                }
                else
                {
                    _logger.LogWarning("Nie znaleziono użytkownika o ID: {UserId}", userId);
                    return RedirectToPage("/Index");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd przy pobieraniu danych użytkownika");
            }

            return Page();
        }
              
        // Zapis danych konta (email, telefon)
        public IActionResult OnPostZapisz()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                _logger.LogWarning("Brak sesji użytkownika przy zapisie danych");
                return RedirectToPage("/Index");
            }

            // Walidacja tylko dla sekcji Konto (pomijamy sekcję Hasło)
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

                string sql = "UPDATE users SET email = @em, telefon = @tel WHERE id = @id";
                using var cmd = new MySqlCommand(sql, db);
                cmd.Parameters.AddWithValue("@em", Konto.Email);
                cmd.Parameters.AddWithValue("@tel", string.IsNullOrWhiteSpace(Konto.Telefon) ? DBNull.Value : Konto.Telefon);
                cmd.Parameters.AddWithValue("@id", userId.Value);

                int rows = cmd.ExecuteNonQuery();
                Komunikat = rows > 0 ? "Zapisano zmiany." : "Brak zmian lub użytkownik nie istnieje.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd przy zapisie danych konta");
                ModelState.AddModelError(string.Empty, "Wystąpił błąd podczas zapisu danych: " + ex.Message);
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

            // Walidacja pól hasła
            ModelState.Clear();
            TryValidateModel(Haslo, nameof(Haslo));

            if (!ModelState.IsValid)
            {
                AktywnaZakladka = "haslo"; // Przełączenie na zakładkę hasła po błędzie
                _logger.LogWarning("Niepoprawny model przy zmianie hasła");
                return Page();
            }

            try
            {
                using var db = new MySqlConnection(_config.GetConnectionString("MySql"));
                db.Open();

                // Odczytaj aktualny hash hasła
                string sql = "SELECT haslo_hash FROM users WHERE id = @id";
                using var cmd = new MySqlCommand(sql, db);
                cmd.Parameters.AddWithValue("@id", userId.Value);
                var hash = cmd.ExecuteScalar()?.ToString();

                // Weryfikacja starego hasła
                if (string.IsNullOrEmpty(hash) || !BCrypt.Net.BCrypt.Verify(Haslo.StareHaslo, hash))
                {
                    AktywnaZakladka = "haslo";
                    ModelState.AddModelError("Haslo.StareHaslo", "Nieprawidłowe obecne hasło.");
                    return Page();
                }

                // Utwórz nowy hash hasła
                string nowyHash = BCrypt.Net.BCrypt.HashPassword(Haslo.NoweHaslo);

                // Aktualizacja w bazie
                string sqlUpdate = "UPDATE users SET haslo_hash = @newHash WHERE id = @id";
                using var updateCmd = new MySqlCommand(sqlUpdate, db);
                updateCmd.Parameters.AddWithValue("@newHash", nowyHash);
                updateCmd.Parameters.AddWithValue("@id", userId.Value);

                int rows = updateCmd.ExecuteNonQuery();

                AktywnaZakladka = "haslo";
                Komunikat = rows > 0 ? "Hasło zostało zmienione." : "Nie udało się zmienić hasła.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd przy zmianie hasła");
                ModelState.AddModelError(string.Empty, "Błąd zmiany hasła: " + ex.Message);
                return Page();
            }

            return RedirectToPage();
        }
    }
}
