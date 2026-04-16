using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ZamekDoDrzwi.Pages
{
 
    // Klasa bazowa dla stron użytkownika (UserPageModel)   
    // Każda strona dziedzicząca po tym modelu wymaga aktywnej sesji
    // i przypisanej roli: "user" lub "admin".
    // W przypadku braku autoryzacji użytkownik zostaje przekierowany
    // na stronę logowania (Index.cshtml).
    public class UserPageModel : PageModel
    {
    
        // Sprawdza, czy użytkownik ma uprawnienia do dostępu do strony.
     
        public override void OnPageHandlerExecuting(PageHandlerExecutingContext context)
        {
            // Pobranie danych sesji – identyfikatora użytkownika i jego roli
            var userId = context.HttpContext.Session.GetInt32("UserId");
            var rola = context.HttpContext.Session.GetString("Rola");
                       
            if (userId == null || string.IsNullOrEmpty(rola) || (rola != "user" && rola != "admin"))
            {
                // Brak uprawnień -> przekierowanie do strony logowania
                context.Result = new RedirectToPageResult("/Index");
            }

            // Wywołanie oryginalnej implementacji klasy bazowej
            base.OnPageHandlerExecuting(context);
        }
        //Uniwersalny handler wylogowania – czyści sesję i przekierowuje do logowania.      
        public IActionResult OnPostLogout()
        {
            // sunięcie wszystkich danych sesyjnych
            HttpContext.Session.Clear();

            // Przekierowanie na stronę główną
            return RedirectToPage("/Index");
        }
    }
}
