using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ZamekDoDrzwi.Pages
{
    // Klasa bazowa dla stron administratora   
    public class AdminPageModel : PageModel
    {        
        // Weryfikacja sesji i roli użytkownika przed wykonaniem akcji (OnGet / OnPost)        
        public override void OnPageHandlerExecuting(PageHandlerExecutingContext context)
        {
            var userId = context.HttpContext.Session.GetInt32("UserId");
            var rola = context.HttpContext.Session.GetString("Rola");

            // Jeśli brak sesji lub rola inna niż admin -> przekierowanie do strony logowania
            if (userId == null || string.IsNullOrEmpty(rola) || rola != "admin")
            {
                context.Result = new RedirectToPageResult("/Index");
            }

            base.OnPageHandlerExecuting(context);
        }
                
        //Obsługuje przycisk "Wyloguj" w panelu administratora
       //Czyści sesję i przekierowuje na stronę główną (logowanie)        
        public IActionResult OnPostLogout()
        {
            // Wyczyść dane sesji użytkownika
            HttpContext.Session.Clear();

            // Przekierowanie na stronę logowania
            return RedirectToPage("/Index");
        }
    }
}
