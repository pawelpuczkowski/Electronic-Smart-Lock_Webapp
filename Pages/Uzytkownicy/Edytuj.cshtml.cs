using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySql.Data.MySqlClient;

namespace ZamekDoDrzwi.Pages.Uzytkownicy
{
    public class EdytujModel : AdminPageModel
    {
        private readonly IConfiguration _config;

        // Konstruktor - pobiera konfiguracje (w tym connection string do bazy)
        public EdytujModel(IConfiguration config)
        {
            _config = config;
        }

        [BindProperty(SupportsGet = true)]
        public int Id { get; set; } // ID użytkownika przekazywane w URL

        [BindProperty] public string Login { get; set; }
        [BindProperty] public string Email { get; set; }
        [BindProperty] public string Telefon { get; set; }
        [BindProperty] public string Rola { get; set; }

        // Komunikaty zwrotne
        public string ErrorMessage { get; set; }
        public string InfoMessage { get; set; }

        // Pobranie danych użytkownika do edycji
        public IActionResult OnGet()
        {
            // Połączenie z bazą
            string dbConnStr = _config.GetConnectionString("MySql");
            using var db = new MySqlConnection(dbConnStr);
            db.Open();

            // Pobranie danych użytkownika po ID
            string sql = "SELECT login, email, telefon, rola FROM users WHERE id = @id";
            using var cmd = new MySqlCommand(sql, db);
            cmd.Parameters.AddWithValue("@id", Id);

            // Wykonanie zapytania i wczytanie danych
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                Login = reader.GetString("login");
                Email = reader.GetString("email");
                Telefon = reader.IsDBNull(reader.GetOrdinal("telefon")) ? "" : reader.GetString("telefon");
                Rola = reader.GetString("rola");
            }
            else
            {
                // Jeśli użytkownik nie istnieje - wróć do listy
                return RedirectToPage("/Uzytkownicy/Lista");
            }

            return Page(); // wyświetlenie formularza
        }

        // Zapis zmian w bazie
        public IActionResult OnPost()
        {
            // walidacja danych (pola obowiązkowe)
            if (string.IsNullOrEmpty(Login) || string.IsNullOrEmpty(Email) || string.IsNullOrEmpty(Rola))
            {
                ErrorMessage = "Wszystkie pola (oprócz telefonu) są wymagane.";
                return Page();
            }

            // Połączenie z bazą
            string dbConnStr = _config.GetConnectionString("MySql");
            using var db = new MySqlConnection(dbConnStr);
            db.Open();

            // SQL UPDATE - aktualizacja danych użytkownika
            string sql = @"
                UPDATE users 
                SET login = @login, 
                    email = @email, 
                    telefon = @telefon, 
                    rola = @rola 
                WHERE id = @id";

            using var cmd = new MySqlCommand(sql, db);
            cmd.Parameters.AddWithValue("@login", Login);
            cmd.Parameters.AddWithValue("@email", Email);
            cmd.Parameters.AddWithValue("@telefon", string.IsNullOrEmpty(Telefon) ? DBNull.Value : Telefon);
            cmd.Parameters.AddWithValue("@rola", Rola);
            cmd.Parameters.AddWithValue("@id", Id);

            // Wykonanie aktualizacji
            cmd.ExecuteNonQuery();

            // Ustaw komunikat o powodzeniu
            InfoMessage = "Dane użytkownika zostały zaktualizowane.";

            // Przekierowanie z powrotem na liste użytkowników
            return RedirectToPage("/Uzytkownicy/Lista");
        }
    }
}
