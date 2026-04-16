using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySql.Data.MySqlClient;
using System.Collections.Generic;

namespace ZamekDoDrzwi.Pages.Uzytkownicy
{
    public class RFIDModel : AdminPageModel
    {
        private readonly IConfiguration _config;

        public RFIDModel(IConfiguration config) => _config = config;

        // Pola powiązane z formularzem
        [BindProperty] public int? WybranyUserId { get; set; }            // ID użytkownika z listy
        [BindProperty] public string RfidCode { get; set; } = string.Empty; // UID karty
        [BindProperty] public string? Opis { get; set; }                   // Opis (np. karta służbowa)
        [BindProperty] public DateTime? DataWaznosci { get; set; }         // Data ważności karty

        // Dane pomocnicze do widoku
        public List<Uzytkownik> Uzytkownicy { get; set; } = new();         // Lista dostępnych użytkowników
        public string InfoMessage { get; set; } = string.Empty;            // Komunikat powodzenia
        public string BadInfoMessage { get; set; } = string.Empty;         // Komunikat błędu

        [BindProperty(SupportsGet = true)]
        public int? kartaId { get; set; }                                  // Jeśli przypisujemy istniejącą kartę


        // Klasa modelu użytkownika (ID + login)
        public class Uzytkownik
        {
            public int Id { get; set; }
            public string Login { get; set; }
        }

        // Wczytanie listy użytkowników i ewentualnie danych karty
        public void OnGet()
        {
            string connStr = _config.GetConnectionString("MySql");
            using var db = new MySqlConnection(connStr);
            db.Open();

            // Załaduj listę użytkowników z bazy
            using var cmd = new MySqlCommand("SELECT id, login FROM users ORDER BY login", db);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                Uzytkownicy.Add(new Uzytkownik
                {
                    Id = reader.GetInt32("id"),
                    Login = reader.GetString("login")
                });
            }

            // Jeśli przekazano ID karty — uzupełnij pole UID w formularzu
            if (kartaId.HasValue)
            {
                reader.Close();
                using var findCard = new MySqlCommand("SELECT uid FROM rfid_cards WHERE id = @id", db);
                findCard.Parameters.AddWithValue("@id", kartaId.Value);
                var result = findCard.ExecuteScalar();
                if (result != null)
                    RfidCode = result.ToString();
            }
        }

        // Obsługa formularza przypisywania karty RFID
        public IActionResult OnPost()
        {
            // Walidacja danych formularza
            if (string.IsNullOrWhiteSpace(RfidCode) || WybranyUserId is null)
            {
                BadInfoMessage = "Wszystkie pola są wymagane.";
                OnGet(); // odśwież dane
                return Page();
            }

            string connStr = _config.GetConnectionString("MySql");
            using var db = new MySqlConnection(connStr);
            db.Open();

            // Jeżeli przekazano ID karty — aktualizujemy istniejący rekord
            if (kartaId.HasValue)
            {
                var updateCmd = new MySqlCommand(@"
                    UPDATE rfid_cards
                    SET user_id = @user,
                        opis = @opis,
                        data_waznosci = @wazna_do,
                        aktywna = 1
                    WHERE id = @id", db);

                updateCmd.Parameters.AddWithValue("@user", WybranyUserId);
                updateCmd.Parameters.AddWithValue("@opis", string.IsNullOrWhiteSpace(Opis) ? DBNull.Value : Opis);
                updateCmd.Parameters.AddWithValue("@wazna_do", DataWaznosci.HasValue ? DataWaznosci.Value : DBNull.Value);
                updateCmd.Parameters.AddWithValue("@id", kartaId.Value);

                updateCmd.ExecuteNonQuery();
                InfoMessage = "Karta RFID została ponownie przypisana.";
            }
            else
            {
                // Sprawdź, czy aktywna karta z takim UID już istnieje
                var checkCmd = new MySqlCommand("SELECT COUNT(*) FROM rfid_cards WHERE uid = @code AND aktywna = 1", db);
                checkCmd.Parameters.AddWithValue("@code", RfidCode);
                bool existsActive = Convert.ToInt32(checkCmd.ExecuteScalar()) > 0;

                if (existsActive)
                {
                    BadInfoMessage = "Aktywna karta z tym UID już istnieje.";
                    OnGet();
                    return Page();
                }

                // Wstaw nową kartę do bazy
                var insertCmd = new MySqlCommand(@"
                    INSERT INTO rfid_cards (user_id, uid, opis, data_waznosci)
                    VALUES (@user, @code, @opis, @wazna_do)", db);

                insertCmd.Parameters.AddWithValue("@user", WybranyUserId);
                insertCmd.Parameters.AddWithValue("@code", RfidCode);
                insertCmd.Parameters.AddWithValue("@opis", string.IsNullOrWhiteSpace(Opis) ? DBNull.Value : Opis);
                insertCmd.Parameters.AddWithValue("@wazna_do", DataWaznosci.HasValue ? DataWaznosci.Value : DBNull.Value);

                insertCmd.ExecuteNonQuery();
                InfoMessage = "Nowa karta RFID została przypisana.";
            }

            // Odśwież widok po zapisaniu
            OnGet();
            return Page();
        }
    }
}
