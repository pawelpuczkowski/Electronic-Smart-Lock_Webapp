using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySql.Data.MySqlClient;

namespace ZamekDoDrzwi.Pages.Uzytkownicy
{
    public class DodajModel : AdminPageModel
    {
        private readonly IConfiguration _config;

        // Konstruktor - wstrzyknięcie konfiguracji (m.in. connection string do bazy danych)
        public DodajModel(IConfiguration config)
        {
            _config = config;
        }

        // Właściwości powiązane z formularzem
        [BindProperty] public string Login { get; set; }
        [BindProperty] public string Email { get; set; }
        [BindProperty] public string Telefon { get; set; }
        [BindProperty] public string Rola { get; set; } = "user";  // domyślnie użytkownik
        [BindProperty] public string Haslo { get; set; }

        // Komunikat informacyjny dla użytkownika (np. błędy, potwierdzenie)
        public string InfoMessage { get; set; }

        // Wywoływany przy pierwszym wejściu na stronę
        public void OnGet() { }

        // Obsługa przeslania formularza
        public IActionResult OnPost()
        {
            // Sprawdzenie, czy wszystkie wymagane pola zostały wypełnione
            if (string.IsNullOrWhiteSpace(Login) ||
                string.IsNullOrWhiteSpace(Email) ||
                string.IsNullOrWhiteSpace(Telefon) ||
                string.IsNullOrWhiteSpace(Haslo))
            {
                InfoMessage = "Wypełnij wszystkie wymagane pola.";
                return Page(); // powrot na strone z komunikatem
            }

            // Haszowanie hasła za pomocą BCrypt przed zapisem w bazie
            string hashed = BCrypt.Net.BCrypt.HashPassword(Haslo);

            // Połączenie z bazą danych
            using var db = new MySqlConnection(_config.GetConnectionString("MySql"));
            db.Open();

            // Przygotowanie zapytania SQL - wstawienie nowego użytkownika
            var dbCmd = new MySqlCommand(@"
                INSERT INTO users (login, email, telefon, rola, haslo_hash, aktywny)
                VALUES (@login, @email, @telefon, @rola, @haslo, 1)", db);

            // Przekazanie parametrów do zapytania (zabezpiecza przed SQL injection)
            dbCmd.Parameters.AddWithValue("@login", Login);
            dbCmd.Parameters.AddWithValue("@email", Email);
            dbCmd.Parameters.AddWithValue("@telefon", string.IsNullOrWhiteSpace(Telefon) ? null : Telefon);
            dbCmd.Parameters.AddWithValue("@rola", Rola);
            dbCmd.Parameters.AddWithValue("@haslo", hashed);

            // Wykonanie zapytania (dodanie użytkownika)
            dbCmd.ExecuteNonQuery();

            // Przekierowanie po dodaniu użytkownika na liste
            return RedirectToPage("/Uzytkownicy/Lista");
        }
    }
}
