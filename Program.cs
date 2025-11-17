using blazor.Components;
using blazor.Components.Data;
using blazor.Components.Servicios;
using Factura.Components;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

String ruta = "mibase_facturas.db";

builder.Configuration.AddInMemoryCollection(new Dictionary<string, string>
{
    { "ConnectionStrings:DefaultConnection", $"Data Source={ruta}" }
});

builder.Services.AddTransient<ServicioFacturas>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    string connectionString = configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    return new ServicioFacturas(connectionString);
});

builder.Services.AddTransient<ServicioControlador>();

var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStatusCodePagesWithReExecute("/404");
app.UseStaticFiles();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

using var conexion = new SqliteConnection($"Data Source={ruta}");
conexion.Open();
var comando = conexion.CreateCommand();
comando.CommandText = """
    CREATE TABLE IF NOT EXISTS Facturas (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        FechaFactura TEXT NOT NULL,
        NombreCliente TEXT NOT NULL
    );
    """;
comando.ExecuteNonQuery();

comando.CommandText = """
    CREATE TABLE IF NOT EXISTS ArticulosFactura (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        FacturaId INTEGER NOT NULL,
        Descripcion TEXT NOT NULL,
        Precio REAL NOT NULL,
        Cantidad INTEGER NOT NULL DEFAULT 1,
        FOREIGN KEY(FacturaId) REFERENCES Facturas(Id)
    );
    """;
comando.ExecuteNonQuery();

// Comprobación automática: si la tabla existe pero le falta la columna Cantidad, la añadimos
try
{
    var checkCmd = conexion.CreateCommand();
    checkCmd.CommandText = "PRAGMA table_info(ArticulosFactura);";
    using var reader = checkCmd.ExecuteReader();
    bool tieneCantidad = false;
    while (reader.Read())
    {
        var nombreCol = reader.GetString(1);
        if (string.Equals(nombreCol, "Cantidad", StringComparison.OrdinalIgnoreCase))
        {
            tieneCantidad = true;
            break;
        }
    }

    if (!tieneCantidad)
    {
        var alterCmd = conexion.CreateCommand();
        alterCmd.CommandText = "ALTER TABLE ArticulosFactura ADD COLUMN Cantidad INTEGER NOT NULL DEFAULT 1;";
        alterCmd.ExecuteNonQuery();
        Console.WriteLine("Migración: columna 'Cantidad' añadida a ArticulosFactura.");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error comprobando/migrando esquema SQLite: {ex.Message}");
}

comando.CommandText = """
    CREATE TABLE IF NOT EXISTS configuracion (
        clave TEXT PRIMARY KEY,
        valor TEXT
    );
    """;
comando.ExecuteNonQuery();

comando.CommandText = """
    INSERT OR IGNORE INTO configuracion (clave, valor) 
    VALUES ('FiltroNombreCliente', '');
    """;
comando.ExecuteNonQuery();

conexion.Close();

app.Run();          