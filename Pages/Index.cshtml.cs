using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySql.Data.MySqlClient;
using System.Net.Http;
using System.Text.Json;
using System.Security.Cryptography;

public class IndexModel : PageModel
{
    // Właściwości formularza logowania
    [BindProperty] public string Login { get; set; } = string.Empty;
    [BindProperty] public string Password { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;

    private readonly IConfiguration _config;
    public IndexModel(IConfiguration config)
    {
        _config = config;
    }

    // Wyświetlenie strony logowania   
    public IActionResult OnGet()
    {
        return Page(); // po prostu pokazuje formularz
    }
        
    // Obsługa logowania użytkownika
    
    public IActionResult OnPost()
    {  
        // Maksymalna liczba błędnych prób i czas blokady konta
        const int MaxAttempts = 5;
        const int LockoutMinutes = 10;

        // Pobranie liczby nieudanych prób i ewentualnej blokady z sesji
        var failedAttempts = HttpContext.Session.GetInt32("FailedAttempts") ?? 0;
        var lockoutUntil = HttpContext.Session.GetString("LockoutUntil");

        // Jeśli użytkownik jest aktualnie zablokowany -> przerwij logowanie
        if (lockoutUntil != null && DateTime.Now < DateTime.Parse(lockoutUntil))
        {
            ErrorMessage = $"Zbyt wiele nieudanych prób. Spróbuj ponownie za jakiś czas.";
            return Page();
        }

        // Walidacja - puste pola
        if (string.IsNullOrWhiteSpace(Login) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Nieprawidłowe dane logowania.";
            return Page();
        }

        try
        {
            // Połączenie z bazą danych
            string connStr = _config.GetConnectionString("MySql");
            using var db = new MySqlConnection(connStr);
            db.Open();

            // Czyszczenie starych lub zużytych tokenów 2FA
            using (var cleanupCmd = new MySqlCommand(
                "DELETE FROM access_tokens WHERE data_waznosci < NOW() OR uzyty = 1", db))
            {
                cleanupCmd.ExecuteNonQuery();
            }

            // Wyszukanie aktywnego użytkownika po loginie lub e-mailu
            using var cmd = new MySqlCommand(
                "SELECT id, email, rola, haslo_hash FROM users WHERE (login = @login OR email = @login) AND aktywny = 1",
                db);
            cmd.Parameters.AddWithValue("@login", Login);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                // Brak użytkownika -> zwiększ licznik prób
                failedAttempts++;
                HttpContext.Session.SetInt32("FailedAttempts", failedAttempts);

                // Jeśli przekroczono limit ustaw blokadę czasową
                if (failedAttempts >= MaxAttempts)
                {
                    var lockoutTime = DateTime.Now.AddMinutes(LockoutMinutes);
                    HttpContext.Session.SetString("LockoutUntil", lockoutTime.ToString());
                    ErrorMessage = $"Zbyt wiele prób. Spróbuj ponownie o {lockoutTime:HH:mm}.";
                }
                else
                {
                    ErrorMessage = "Nieprawidłowe dane logowania.";
                }

                return Page();
            }

            // Dane użytkownika
            int userId = reader.GetInt32("id");
            string email = reader.GetString("email");
            string rola = reader.GetString("rola");
            string hash = reader.GetString("haslo_hash");

            // Weryfikacja hasła z użyciem BCrypt
            if (!BCrypt.Net.BCrypt.Verify(Password, hash))
            {
                failedAttempts++;
                HttpContext.Session.SetInt32("FailedAttempts", failedAttempts);

                if (failedAttempts >= MaxAttempts)
                {
                    var lockoutTime = DateTime.Now.AddMinutes(LockoutMinutes);
                    HttpContext.Session.SetString("LockoutUntil", lockoutTime.ToString());
                    ErrorMessage = $"Zbyt wiele prób. Spróbuj ponownie o {lockoutTime:HH:mm}.";
                }
                else
                {
                    ErrorMessage = "Nieprawidłowe dane logowania.";
                }

                return Page();
            }

            reader.Close();

            // Logowanie poprawne -> wyczyść liczniki błędów
            HttpContext.Session.Remove("FailedAttempts");
            HttpContext.Session.Remove("LockoutUntil");
         
            // Generowanie kodu 2FA
            string code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

            // Zapis kodu do tabeli access_tokens (ważny 5 minut)
            using (var insertCmd = new MySqlCommand(
                "INSERT INTO access_tokens (user_id, kod, data_waznosci) VALUES (@userId, @kod, @wazny_do)",
                db))
            {
                insertCmd.Parameters.AddWithValue("@userId", userId);
                insertCmd.Parameters.AddWithValue("@kod", code);
                insertCmd.Parameters.AddWithValue("@wazny_do", DateTime.Now.AddMinutes(5));
                insertCmd.ExecuteNonQuery();
            }
                       
            // Wysyłka kodu e-mailem       
            EmailService.Send(
                email,
                "Weryfikacja dwuetapowa",
                $"""
                Ktoś właśnie próbuje zalogować się na Twoje konto.

                Jeśli to Ty, wpisz ten kod w ciągu 5 minut:
                Kod 2FA: {code}

                Jeśli to nie Ty, zignoruj tą wiadomość lub zmień hasło.
                """);
            
            // Przechowanie danych w sesji          
            HttpContext.Session.SetInt32("Pending2FA_UserId", userId);
            HttpContext.Session.SetString("Pending2FA_Email", email);
            HttpContext.Session.SetString("Pending2FA_Rola", rola);

            // Przekierowanie do strony weryfikacji kodu 2FA
            return RedirectToPage("/Verify2FA");
        }
        catch (Exception ex)
        {
            // Obsługa błędów serwera
            System.Diagnostics.Debug.WriteLine(">>> Błąd logowania: " + ex.Message);
            ErrorMessage = "Błąd serwera. Spróbuj poniej.";
            return Page();
        }
    }
}
