using blazor.Components.Data;
using FacturaModel = blazor.Components.Data.Factura;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace blazor.Components.Servicios
{
    public class ServicioFacturas
    {
        private readonly string _connectionString;

        public ServicioFacturas(string connectionString)
        {
            _connectionString = connectionString;
        }

        private SqliteConnection GetConnection() => new SqliteConnection(_connectionString);

        public async Task<string> ObtenerConfiguracionAsync(string clave)
        {
            using var conexion = GetConnection();
            await conexion.OpenAsync();

            var comando = conexion.CreateCommand();
            comando.CommandText = "SELECT valor FROM configuracion WHERE clave = @clave";
            comando.Parameters.AddWithValue("@clave", clave);

            var resultado = await comando.ExecuteScalarAsync();
            return resultado?.ToString() ?? string.Empty;
        }

        public async Task GuardarConfiguracionAsync(string clave, string valor)
        {
            using var conexion = GetConnection();
            await conexion.OpenAsync();

            var comando = conexion.CreateCommand();
            comando.CommandText = "INSERT OR REPLACE INTO configuracion (clave, valor) VALUES (@clave, @valor)";
            comando.Parameters.AddWithValue("@clave", clave);
            comando.Parameters.AddWithValue("@valor", valor);

            await comando.ExecuteNonQueryAsync();
        }

        public async Task<IEnumerable<blazor.Components.Data.Factura>> ObtenerTodasAsync()
        {
            List<blazor.Components.Data.Factura> facturas = new();

            using var conexion = GetConnection();
            await conexion.OpenAsync();

            var comando = conexion.CreateCommand();
            comando.CommandText = "SELECT Id, FechaFactura, NombreCliente FROM Facturas ORDER BY Id DESC";

            using var reader = await comando.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var factura = new blazor.Components.Data.Factura                    
                {
                    Id = reader.GetInt32(0),
                    FechaFactura = DateTime.Parse(reader.GetString(1)),
                    NombreCliente = reader.GetString(2),
                    Articulos = new List<blazor.Components.Data.Factura.ArticuloFactura>()
                };

                factura.Articulos.AddRange(
                    await ObtenerArticulosPorFacturaIdAsync(factura.Id, conexion)
                );

                facturas.Add(factura);
            }

            return facturas;
        }

        public async Task<blazor.Components.Data.Factura?> ObtenerPorIdAsync(int id)
        {
            blazor.Components.Data.Factura? factura = null;

            using var conexion = GetConnection();
            await conexion.OpenAsync();

            var comando = conexion.CreateCommand();
            comando.CommandText = "SELECT Id, FechaFactura, NombreCliente FROM Facturas WHERE Id = @id";
            comando.Parameters.AddWithValue("@id", id);

            using (var reader = await comando.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    factura = new blazor.Components.Data.Factura
                    {
                        Id = reader.GetInt32(0),
                        FechaFactura = DateTime.Parse(reader.GetString(1)),
                        NombreCliente = reader.GetString(2),
                        Articulos = new List<blazor.Components.Data.Factura.ArticuloFactura>()
                    };
                }
            }

            if (factura != null)
            {
                factura.Articulos.AddRange(
                    await ObtenerArticulosPorFacturaIdAsync(id, conexion)
                );
            }

            return factura;
        }

        private async Task<IEnumerable<blazor.Components.Data.Factura.ArticuloFactura>> ObtenerArticulosPorFacturaIdAsync(int facturaId, SqliteConnection conexion)
        {
            List<blazor.Components.Data.Factura.ArticuloFactura> articulos = new();

            var comando = conexion.CreateCommand();
            // Aseguramos que leemos también la cantidad
            comando.CommandText = "SELECT Id, Descripcion, Cantidad, Precio FROM ArticulosFactura WHERE FacturaId = @facturaId";
            comando.Parameters.Clear();
            comando.Parameters.AddWithValue("@facturaId", facturaId);

            using var reader = await comando.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                // Lectura segura del precio (SQLite suele devolver double para tipos numéricos)
                decimal precio = 0m;
                var precioObj = reader.GetValue(3);
                if (precioObj is double d) precio = Convert.ToDecimal(d);
                else if (precioObj is decimal m) precio = m;
                else precio = Convert.ToDecimal(precioObj);

                articulos.Add(new blazor.Components.Data.Factura.ArticuloFactura
                {
                    Id = reader.GetInt32(0),
                    Descripcion = reader.GetString(1),
                    Cantidad = reader.IsDBNull(2) ? 1 : reader.GetInt32(2),
                    Precio = precio,
                    FacturaId = facturaId
                });
            }

            return articulos;
        }

        public async Task AgregarFacturaAsync(blazor.Components.Data.Factura nuevaFactura)
        {
            using var conexion = GetConnection();
            await conexion.OpenAsync();

            using SqliteTransaction transaccion = conexion.BeginTransaction();
            try
            {
                var comandoFactura = conexion.CreateCommand();
                comandoFactura.Transaction = transaccion;
                comandoFactura.CommandText = @"
                    INSERT INTO Facturas (FechaFactura, NombreCliente) 
                    VALUES (@fecha, @cliente);
                    SELECT last_insert_rowid();";

                comandoFactura.Parameters.AddWithValue("@fecha", nuevaFactura.FechaFactura.ToString("yyyy-MM-dd HH:mm:ss"));
                comandoFactura.Parameters.AddWithValue("@cliente", nuevaFactura.NombreCliente);

                long facturaId = (long)(await comandoFactura.ExecuteScalarAsync())!;
                nuevaFactura.Id = (int)facturaId;

                if (nuevaFactura.Articulos.Any())
                {
                    foreach (var articulo in nuevaFactura.Articulos)
                    {
                        var comandoArticulo = conexion.CreateCommand();
                        comandoArticulo.Transaction = transaccion;
                        comandoArticulo.CommandText = @"
                            INSERT INTO ArticulosFactura (FacturaId, Descripcion, Precio, Cantidad)
                            VALUES (@facturaId, @desc, @precio, @cantidad)";

                        comandoArticulo.Parameters.AddWithValue("@facturaId", facturaId);
                        comandoArticulo.Parameters.AddWithValue("@desc", articulo.Descripcion);
                        comandoArticulo.Parameters.AddWithValue("@precio", articulo.Precio);
                        comandoArticulo.Parameters.AddWithValue("@cantidad", articulo.Cantidad);
                        await comandoArticulo.ExecuteNonQueryAsync();
                    }
                }

                await transaccion.CommitAsync();
            }
            catch
            {
                await transaccion.RollbackAsync();
                throw;
            }
        }

        public async Task ActualizarFacturaAsync(blazor.Components.Data.Factura facturaEditada)
        {
            using var conexion = GetConnection();
            await conexion.OpenAsync();

            using SqliteTransaction transaccion = conexion.BeginTransaction();
            try
            {
                var comandoFactura = conexion.CreateCommand();
                comandoFactura.Transaction = transaccion;
                comandoFactura.CommandText = @"
                    UPDATE Facturas 
                    SET FechaFactura = @fecha, NombreCliente = @cliente 
                    WHERE Id = @id";

                comandoFactura.Parameters.AddWithValue("@id", facturaEditada.Id);
                comandoFactura.Parameters.AddWithValue("@fecha", facturaEditada.FechaFactura.ToString("yyyy-MM-dd HH:mm:ss"));
                comandoFactura.Parameters.AddWithValue("@cliente", facturaEditada.NombreCliente);

                await comandoFactura.ExecuteNonQueryAsync();

                var comandoBorrarArticulos = conexion.CreateCommand();
                comandoBorrarArticulos.Transaction = transaccion;
                comandoBorrarArticulos.CommandText = "DELETE FROM ArticulosFactura WHERE FacturaId = @facturaId";
                comandoBorrarArticulos.Parameters.AddWithValue("@facturaId", facturaEditada.Id);
                await comandoBorrarArticulos.ExecuteNonQueryAsync();

                foreach (var articulo in facturaEditada.Articulos)
                {
                    var comandoArticulo = conexion.CreateCommand();
                    comandoArticulo.Transaction = transaccion;
                    comandoArticulo.CommandText = @"
                        INSERT INTO ArticulosFactura (FacturaId, Descripcion, Precio, Cantidad)
                        VALUES (@facturaId, @desc, @precio, @cantidad)";

                    comandoArticulo.Parameters.AddWithValue("@facturaId", facturaEditada.Id);
                    comandoArticulo.Parameters.AddWithValue("@desc", articulo.Descripcion);
                    comandoArticulo.Parameters.AddWithValue("@precio", articulo.Precio);
                    comandoArticulo.Parameters.AddWithValue("@cantidad", articulo.Cantidad);
                    await comandoArticulo.ExecuteNonQueryAsync();
                }

                await transaccion.CommitAsync();
            }
            catch
            {
                await transaccion.RollbackAsync();
                throw;
            }
        }

        public async Task EliminarFacturaAsync(int facturaId)
        {
            using var conexion = GetConnection();
            await conexion.OpenAsync();

            using SqliteTransaction transaccion = conexion.BeginTransaction();
            try
            {
                var comandoBorrarArticulos = conexion.CreateCommand();
                comandoBorrarArticulos.Transaction = transaccion;
                comandoBorrarArticulos.CommandText = "DELETE FROM ArticulosFactura WHERE FacturaId = @facturaId";
                comandoBorrarArticulos.Parameters.AddWithValue("@facturaId", facturaId);
                await comandoBorrarArticulos.ExecuteNonQueryAsync();

                var comandoBorrarFactura = conexion.CreateCommand();
                comandoBorrarFactura.Transaction = transaccion;
                comandoBorrarFactura.CommandText = "DELETE FROM Facturas WHERE Id = @facturaId";
                comandoBorrarFactura.Parameters.AddWithValue("@facturaId", facturaId);
                await comandoBorrarFactura.ExecuteNonQueryAsync();

                await transaccion.CommitAsync();
            }
            catch
            {
                await transaccion.RollbackAsync();
                throw;
            }
        }
    }
}