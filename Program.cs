using MSFSSHTTP.Middleware;
using MSFSSHTTP.Services;
using SoapCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSoapCore();
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<IMSFSSHTTPService, MSFSSHTTPService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    //app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Rewrite /_vti_bin/ requests BEFORE routing so attribute-routed
// FSSHTTP controllers (CellStorage, SharedAccess, etc.) match cleanly.
// Must be registered before UseRouting().
app.UseVtiBinRouting();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// WebDAV catch-all: lowest priority, only matched when no attribute route (CellStorage, SharedAccess) wins.
// Handles GET, HEAD, OPTIONS, PUT, LOCK, UNLOCK, PROPFIND for document file access.
app.MapControllerRoute(
    name: "webdav_files",
    pattern: "FSSHTTP/{action}/{*path}",
    defaults: new { controller = "WebDav", action = "GetDoc", path = "" });

app.Run();
