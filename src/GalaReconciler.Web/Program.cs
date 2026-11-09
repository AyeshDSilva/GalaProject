using GalaReconciler.Web.Features.Reconciliation;
using GalaReconciler.Web.Shared.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllersWithViews()
    .AddRazorOptions(options => options.ViewLocationExpanders.Add(new FeatureViewLocationExpander()));

builder.Services.AddReconciliation(builder.Configuration);

var app = builder.Build();

await app.InitializeReconciliationAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Error/{0}");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Reconciliation}/{action=Index}/{id?}");
app.MapReconciliationHub();

app.Run();

public partial class Program;
