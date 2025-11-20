using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Factura.Components;
using blazor.Components.Servicios;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

string ruta = "mibase_facturas.db";

builder.Configuration.AddInMemoryCollection(new Dictionary<string, string>
{
    { "ConnectionStrings:DefaultConnection", $"Data Source={ruta}" }
});

builder.Services.AddTransient<ServicioFacturas>(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    string cs = cfg.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    return new ServicioFacturas(cs);
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

using var cx = new SqliteConnection($"Data Source={ruta}");
cx.Open();

var cmd = cx.CreateCommand();

cmd.CommandText = """
CREATE TABLE IF NOT EXISTS Facturas (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    FechaFactura TEXT NOT NULL,
    NombreCliente TEXT NOT NULL
);
""";
cmd.ExecuteNonQuery();

cmd.CommandText = """
CREATE TABLE IF NOT EXISTS ArticulosFactura (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    FacturaId INTEGER NOT NULL,
    Descripcion TEXT NOT NULL,
    Precio REAL NOT NULL,
    Cantidad INTEGER NOT NULL DEFAULT 1,
    FOREIGN KEY(FacturaId) REFERENCES Facturas(Id)
);
""";
cmd.ExecuteNonQuery();

cmd.CommandText = """
CREATE TABLE IF NOT EXISTS configuracion (
    clave TEXT PRIMARY KEY,
    valor TEXT
);
""";
cmd.ExecuteNonQuery();

cmd.CommandText = """
INSERT OR IGNORE INTO configuracion (clave, valor)
VALUES ('FiltroNombreCliente', '');
""";
cmd.ExecuteNonQuery();

var chk = cx.CreateCommand();
chk.CommandText = "SELECT COUNT(*) FROM Facturas;";
long total = (long)chk.ExecuteScalar();

if (total == 0)
{
    cmd.CommandText = """
    INSERT INTO Facturas (FechaFactura, NombreCliente) VALUES
    ('2025-01-03','Luis Martínez'),
    ('2025-01-05','Ana Torres'),
    ('2025-01-06','Carlos Vega'),
    ('2025-01-08','Fernanda López'),
    ('2025-01-09','Pedro Sánchez');
    """;
    cmd.ExecuteNonQuery();

    cmd.CommandText = """
INSERT INTO ArticulosFactura (FacturaId,Descripcion,Precio,Cantidad) VALUES
(1,'Tesla Model S',1740000,1),
(1,'Tesla Model 3',875000,2),
(2,'BMW M5',2390000,1),
(3,'Mercedes AMG GT',4200000,1),
(4,'BMW X6',1850000,1),
(5,'Tesla Model Y',1100000,3),

-- Casas y bienes raíces
(3,'Casa residencial',4500000,1),
(4,'Departamento en playa',6200000,1),

-- Gasolina
(1,'Gasolina Premium',25,40),
(2,'Gasolina Magna',22,50),
(5,'Gasolina Diesel',24,80),

-- Comida y restaurantes
(1,'Cena lujo restaurante',3500,2),
(3,'Sushi Omakase',4200,3),
(4,'Buffet Gourmet',1800,4),

-- Compras de lujo
(1,'Rolex Submariner',215000,1),
(2,'Louis Vuitton Bolsa',58000,1),
(5,'Gucci Sneakers',35000,2);
""";
    cmd.ExecuteNonQuery();

}

cx.Close();

app.Run();
