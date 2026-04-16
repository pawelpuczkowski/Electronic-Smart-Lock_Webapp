using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySql.Data.MySqlClient;

public class ResetujHasloModel : PageModel
{
    private readonly IConfiguration _config;

    public ResetujHasloModel(IConfiguration config)
    {
        _config = config;
    }
       

    [BindProperty]
    public string Email { get; set; } = string.Empty;  // Adres e-mail wpisany przez użytkownika

    public string InfoMessage { get; set; } = string.Empty; // Komunikat informacyjny (sukces/błąd)

    // Wyświetlenie formularza resetowania   
    public void OnGet() { }
    
    // Wysyłka linku resetującego hasło  
    public IActionResult OnPost()
    {
        // Walidacja – sprawdzenie, czy pole e-mail nie jest puste
        if (string.IsNullOrWhiteSpace(Email))
        {
            InfoMessage = "Podaj adres e-mail.";
            return Page();
        }

        // Połączenie z bazą danych
        string connStr = _config.GetConnectionString("MySql");
        using var db = new MySqlConnection(connStr);
        db.Open();

        // 🔹 Sprawdzenie, czy w bazie istnieje użytkownik o tym e-mailu
        string query = "SELECT id FROM users WHERE email = @Email";
        using var cmd = new MySqlCommand(query, db);
        cmd.Parameters.AddWithValue("@Email", Email);

        var userId = cmd.ExecuteScalar();

        // Jeśli nie znaleziono użytkownika → zwracamy ogólny komunikat (dla bezpieczeństwa)
        if (userId == null)
        {
            InfoMessage = "Jeśli e-mail istnieje w systemie, wyślemy link resetujący.";
            return Page();
        }

        // Generowanie unikalnego tokenu resetującego (GUID)
        string token = Guid.NewGuid().ToString();
        DateTime expires = DateTime.Now.AddMinutes(30); // ważność 30 minut

        // Zapis tokenu do tabeli `password_resets`
        string insert = @"
            INSERT INTO password_resets (user_id, token, expires_at)
            VALUES (@UserId, @Token, @Expires)";
        using var insertCmd = new MySqlCommand(insert, db);
        insertCmd.Parameters.AddWithValue("@UserId", userId);
        insertCmd.Parameters.AddWithValue("@Token", token);
        insertCmd.Parameters.AddWithValue("@Expires", expires);
        insertCmd.ExecuteNonQuery();

        // Przygotowanie linku resetującego – odwołuje się do strony /UstawNoweHaslo
        string resetUrl = Url.Page(
            "/UstawNoweHaslo",  // docelowa strona
            null,               // brak handlera
            new { token = token }, // parametry URL (token)
            Request.Scheme      
        );

        // Treść wiadomości e-mail wysyłanej użytkownikowi
        string body = $@"
Otrzymaliśmy prośbę o zresetowanie hasła do Twojego konta.

Kliknij poniższy link, aby ustawić nowe hasło:
{resetUrl}

Jeśli to nie Ty, zignoruj tę wiadomość. Hasło nie zostanie zmienione bez Twojej interakcji.";
        
        // Wysyłka wiadomości e-mail z linkiem resetującym
        try
        {
            // Użycie serwisu EmailService – centralna obsługa SMTP
            EmailService.Send(Email, "Reset hasła – Zamek do drzwi", body);
        }
        catch (Exception)
        {
            // Obsługa błędu np. przy braku połączenia z serwerem poczty
            InfoMessage = "Wystąpił problem z wysyłką e-maila.";
            return Page();
        }

        //  Komunikat ogólny (nie zdradza, czy adres e-mail faktycznie istnieje)
        InfoMessage = "Jeśli e-mail istnieje, wysłaliśmy link resetujący hasło.";
        return Page();
    }
}
