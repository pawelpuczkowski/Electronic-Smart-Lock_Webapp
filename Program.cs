using System.Text;
using ZamekDoDrzwi.Services;
using Microsoft.Extensions.FileProviders; 
using System.IO;

//Rejestracja dostawcy stron kodowych i ustawienie UTF-8 jako globalnego kodowania
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
Console.OutputEncoding = Encoding.UTF8;
var builder = WebApplication.CreateBuilder(args);

// Dodanie usług
builder.Services.AddRazorPages();
builder.Services.AddSingleton<MqttWorker>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<MqttWorker>());


// Widoczność kontrolerów API
builder.Services.AddControllers();
builder.Services.AddHostedService<ZamekDoDrzwi.Services.NotificationWorker>();
builder.Services.AddHostedService<ZamekDoDrzwi.Services.NotificationDispatcher>();
builder.Services.AddDistributedMemoryCache(); // wymagane przez Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddHttpContextAccessor(); // opcjonalne –  jeśli używam IHttpContextAccessor
var app = builder.Build();

// Obsługa błędów i HTTPS
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
// Obsługa folderu „uploads”
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "uploads")),
    RequestPath = "/uploads"
});

app.UseRouting();

//najpierw sesja!
app.UseSession();
app.UseAuthorization();

//Uruchom kontrolery API
app.MapControllers();
app.MapRazorPages();

app.Run();
