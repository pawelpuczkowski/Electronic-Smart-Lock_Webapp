using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using System.Collections.Generic;

namespace ZamekDoDrzwi.Pages.Uzytkownicy
{
    public class ListaModel : AdminPageModel
    {
        private readonly IConfiguration _config;

        // Konstruktor – pobiera konfigurację
        public ListaModel(IConfiguration config)
        {
            _config = config;
        }

        // Lista użytkowników wyświetlana w tabeli
        public List<Uzytkownik> Uzytkownicy { get; set; } = new();

        // Pobranie wszystkich użytkowników z bazy danych
        public void OnGet()
        {
            string connStr = _config.GetConnectionString("MySql");
            using var db = new MySqlConnection(connStr);
            db.Open();

            // Zapytanie SQL pobierające dane użytkowników
            string sql = "SELECT id, login, email, telefon, rola, aktywny FROM users";
            using var cmd = new MySqlCommand(sql, db);
            using var reader = cmd.ExecuteReader();

            // Wypełnienie listy obiektami klasy Uzytkownik
            while (reader.Read())
            {
                Uzytkownicy.Add(new Uzytkownik
                {
                    Id = reader.GetInt32("id"),
                    Login = reader.GetString("login"),
                    Email = reader.GetString("email"),
                    Telefon = reader.IsDBNull(reader.GetOrdinal("telefon")) ? "" : reader.GetString("telefon"),
                    Rola = reader.GetString("rola"),
                    Aktywny = reader.GetBoolean("aktywny")
                });
            }
        }

        // Klasa pomocnicza opisująca użytkownika
        public class Uzytkownik
        {
            public int Id { get; set; }
            public string Login { get; set; }
            public string Email { get; set; }
            public string Telefon { get; set; }
            public string Rola { get; set; }
            public bool Aktywny { get; set; }
        }

        // Zablokowanie lub odblokowanie konta użytkownika
        public IActionResult OnPostBlokuj(int id)
        {
            string connStr = _config.GetConnectionString("MySql");
            using var db = new MySqlConnection(connStr);
            db.Open();

            // Sprawdź aktualny status użytkownika
            string selectSql = "SELECT aktywny FROM users WHERE id = @id";
            using var selectCmd = new MySqlCommand(selectSql, db);
            selectCmd.Parameters.AddWithValue("@id", id);

            // Uniemożliwiamy zablokowanie własnego konta
            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (id == currentUserId)
            {
                TempData["Error"] = "Nie możesz zablokować własnego konta.";
                return RedirectToPage();
            }

            var wynik = selectCmd.ExecuteScalar();
            if (wynik == null) return NotFound();

            bool aktywny = Convert.ToBoolean(wynik);
            bool nowyStatus = !aktywny;

            // Aktualizacja statusu w bazie
            string updateSql = "UPDATE users SET aktywny = @aktywny WHERE id = @id";
            using var updateCmd = new MySqlCommand(updateSql, db);
            updateCmd.Parameters.AddWithValue("@aktywny", nowyStatus);
            updateCmd.Parameters.AddWithValue("@id", id);
            updateCmd.ExecuteNonQuery();

            TempData["Info"] = nowyStatus ? "Użytkownik został odblokowany." : "Użytkownik został zablokowany.";
            return RedirectToPage(); // Odśwież stronę
        }

        // Usunięcie użytkownika z bazy
        public IActionResult OnPostUsun(int id)
        {
            string connStr = _config.GetConnectionString("MySql");
            using var db = new MySqlConnection(connStr);
            db.Open();

            // Sprawdź, czy użytkownik istnieje
            string selectSql = "SELECT id FROM users WHERE id = @id";
            using var selectCmd = new MySqlCommand(selectSql, db);
            selectCmd.Parameters.AddWithValue("@id", id);
            var wynik = selectCmd.ExecuteScalar();

            if (wynik == null) return NotFound();

            // Zabezpieczenie przed usunięciem samego siebie
            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (id == currentUserId)
            {
                TempData["Error"] = "Nie możesz usunąć własnego konta.";
                return RedirectToPage();
            }

            // Usuń użytkownika z bazy
            string deleteSql = "DELETE FROM users WHERE id = @id";
            using var deleteCmd = new MySqlCommand(deleteSql, db);
            deleteCmd.Parameters.AddWithValue("@id", id);
            deleteCmd.ExecuteNonQuery();

            TempData["Info"] = "Użytkownik został usunięty.";
            return RedirectToPage(); // Odśwież stronę
        }
    }
}
