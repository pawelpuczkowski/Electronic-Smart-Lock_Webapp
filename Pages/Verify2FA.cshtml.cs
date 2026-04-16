using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySql.Data.MySqlClient;

namespace ZamekDoDrzwi.Pages
{    
    public class Verify2FAModel : PageModel
    {
        // Właściwość powiązana z polem w formularzu (kod 2FA)
        [BindProperty] public string Code { get; set; } = string.Empty;

        // Komunikat o błędzie (wyświetlany w widoku)
        public string ErrorMessage { get; set; } = string.Empty;

        private readonly IConfiguration _config;

        public Verify2FAModel(IConfiguration config)
        {
            _config = config;
        }
     
        // Sprawdza, czy istnieje tymczasowa sesja 2FA        
        public IActionResult OnGet()
        {
            // Jeśli brak tymczasowych danych - wróć do logowania
            if (HttpContext.Session.GetInt32("Pending2FA_UserId") == null)
                return RedirectToPage("/Index");

            return Page();
        }
              
        // Weryfikacja kodu 2FA w bazie danych        
        public IActionResult OnPost()
        {
            // Maksymalna liczba prób i czas blokady w minutach
            const int MaxAttempts = 5;
            const int LockoutMinutes = 10;

            // Pobierz dane tymczasowe z sesji
            var userId = HttpContext.Session.GetInt32("Pending2FA_UserId");
            var email = HttpContext.Session.GetString("Pending2FA_Email");
            var rola = HttpContext.Session.GetString("Pending2FA_Rola");

            // Jeśli brakuje danych - wróć do logowania
            if (userId == null || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(rola))
                return RedirectToPage("/Index");

            // Obsługa limitu błędnych prób            
            var failedAttempts = HttpContext.Session.GetInt32("2FA_FailedAttempts") ?? 0;
            var lockoutUntil = HttpContext.Session.GetString("2FA_LockoutUntil");

            // Jeśli konto tymczasowo zablokowane - komunikat i koniec
            if (lockoutUntil is not null && DateTime.Now < DateTime.Parse(lockoutUntil))
            {
                ErrorMessage = $"Zbyt wiele nieudanych prób. Spróbuj ponownie o {DateTime.Parse(lockoutUntil):HH:mm}.";
                return Page();
            }

           
            //Sprawdzenie kodu 2FA w bazie danych
           
            try
            {
                string connStr = _config.GetConnectionString("MySql");
                using var db = new MySqlConnection(connStr);
                db.Open();

                string query = @"
                    SELECT id
                    FROM access_tokens
                    WHERE user_id = @userId
                      AND kod = @code
                      AND uzyty = 0
                      AND data_waznosci > NOW()";

                using var cmd = new MySqlCommand(query, db);
                cmd.Parameters.AddWithValue("@userId", userId.Value);
                cmd.Parameters.AddWithValue("@code", Code);

                using var reader = cmd.ExecuteReader();

                // Jeśli nie znaleziono pasującego kodu - błąd
                if (!reader.Read())
                {
                    failedAttempts++;
                    HttpContext.Session.SetInt32("2FA_FailedAttempts", failedAttempts);

                    if (failedAttempts >= MaxAttempts)
                    {
                        var lockoutTime = DateTime.Now.AddMinutes(LockoutMinutes);
                        HttpContext.Session.SetString("2FA_LockoutUntil", lockoutTime.ToString());
                        ErrorMessage = $"Zbyt wiele prób. Spróbuj ponownie o {lockoutTime:HH:mm}.";
                    }
                    else
                    {
                        ErrorMessage = "Nieprawidłowy lub wygasły kod.";
                    }

                    return Page();
                }

                // Jeśli kod poprawny - pobierz ID tokenu
                int tokenId = reader.GetInt32("id");
                reader.Close();

                // Oznacz kod jako użyty (aby nie mógł być ponownie wykorzystany)
                using var updateCmd = new MySqlCommand(
                    "UPDATE access_tokens SET uzyty = 1 WHERE id = @id", db);
                updateCmd.Parameters.AddWithValue("@id", tokenId);
                updateCmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                // Obsługa błędów połączenia z bazą
                ErrorMessage = "Błąd połączenia z bazą danych.";
                Console.WriteLine($"Błąd MySQL: {ex.Message}");
                return Page();
            }
          
            // Sukces - pełne zalogowanie użytkownika           

            // Zapisz dane sesji użytkownika
            HttpContext.Session.SetInt32("UserId", userId.Value);
            HttpContext.Session.SetString("Email", email);
            HttpContext.Session.SetString("Rola", rola);

            // Usuń dane tymczasowe po zakończeniu weryfikacji
            HttpContext.Session.Remove("Pending2FA_UserId");
            HttpContext.Session.Remove("Pending2FA_Email");
            HttpContext.Session.Remove("Pending2FA_Rola");
            HttpContext.Session.Remove("2FA_FailedAttempts");
            HttpContext.Session.Remove("2FA_LockoutUntil");

            // Przekierowanie do właściwego panelu w zależności od roli
            return rola switch
            {
                "admin" => RedirectToPage("/Dashboard"),
                "user" => RedirectToPage("/Uzytkownik/Index"),
                _ => RedirectToPage("/Index") // awaryjne przekierowanie
            };
        }
    }
}
