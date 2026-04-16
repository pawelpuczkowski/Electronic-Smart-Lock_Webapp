using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySql.Data.MySqlClient;
public class UstawNoweHasloModel : PageModel
{
    private readonly IConfiguration _config;

    public UstawNoweHasloModel(IConfiguration config)
    {
        _config = config;
    }   

    [BindProperty(SupportsGet = true)]
    public string Token { get; set; } = string.Empty; // Token przekazany w URL

    [BindProperty]
    public string NoweHaslo { get; set; } = string.Empty; // Hasło wpisane przez użytkownika

    [BindProperty]
    public string PowtorzHaslo { get; set; } = string.Empty; // Powtórzone hasło (dla walidacji)

    public string InfoMessage { get; set; } = string.Empty;   // Komunikat o sukcesie
    public string BadInfoMessage { get; set; } = string.Empty; // Komunikat o błędzie


    // Zapis nowego hasła    
    public IActionResult OnPost()
    {
        // Walidacja pól formularza – sprawdzenie zgodności haseł
        if (NoweHaslo != PowtorzHaslo)
        {
            BadInfoMessage = "Hasła nie są identyczne.";
            return Page();
        }

        // Nawiązanie połączenia z bazą danych
        string connStr = _config.GetConnectionString("MySql");
        using var db = new MySqlConnection(connStr);
        db.Open();

        // 3️Weryfikacja tokenu resetującego
        //    - musi istnieć w tabeli `password_resets`
        //    - musi być jeszcze ważny (`expires_at > NOW()`)
        //    - nie może być wcześniej użyty (`used = 0`)
        string query = @"
            SELECT user_id
            FROM password_resets
            WHERE token = @Token
              AND expires_at > NOW()
              AND used = 0";
        using var cmd = new MySqlCommand(query, db);
        cmd.Parameters.AddWithValue("@Token", Token);

        var userId = cmd.ExecuteScalar();

        // Jeśli token nie istnieje lub wygasł -> komunikat o błędzie
        if (userId == null)
        {
            BadInfoMessage = "Nieprawidłowy lub wygasły token.";
            return Page();
        }

        // Aktualizacja hasła użytkownika
        //    - generowanie nowego hash'a metodą BCrypt
        //    - zapis nowego hash'a do kolumny `haslo_hash`
        string hashed = BCrypt.Net.BCrypt.HashPassword(NoweHaslo);
        using var update = new MySqlCommand(
            "UPDATE users SET haslo_hash = @Haslo WHERE id = @Id", db);
        update.Parameters.AddWithValue("@Haslo", hashed);
        update.Parameters.AddWithValue("@Id", userId);
        update.ExecuteNonQuery();

        // Oznaczenie tokenu jako użytego, aby nie można było go ponownie wykorzystać
        using var markUsed = new MySqlCommand(
            "UPDATE password_resets SET used = 1 WHERE token = @Token", db);
        markUsed.Parameters.AddWithValue("@Token", Token);
        markUsed.ExecuteNonQuery();

        // Przekierowanie po sukcesie – komunikat o powodzeniu
        TempData["InfoMessage"] = "Hasło zostało zmienione. Zaloguj się ponownie.";
        return RedirectToPage("/Index");
    }
}
