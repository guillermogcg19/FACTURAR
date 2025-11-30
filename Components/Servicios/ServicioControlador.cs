using blazor.Components.Data;
using FacturaModel = blazor.Components.Data.Factura;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace blazor.Components.Servicios
{
    public enum CriterioOrdenacion
    {
        FechaDescendente,
        IdDescendente,
        NombreClienteAscendente
    }

    public class ServicioControlador
    {
        private readonly ServicioFacturas _servicioFacturas;

        private const string CLAVE_FILTRO_CLIENTE = "FiltroNombreCliente";
        private const string CLAVE_ORDENACION = "CriterioOrdenacion";

        public string FiltroNombreCliente { get; set; } = string.Empty;
        public CriterioOrdenacion OrdenacionSeleccionada { get; set; } = CriterioOrdenacion.FechaDescendente;

        private List<FacturaModel> _facturasEnMemoria = new();

        public ServicioControlador(ServicioFacturas servicioFacturas)
        {
            _servicioFacturas = servicioFacturas;
        }

        // -------------------------------------------------------------
        // CARGA DE CONFIGURACIÓN
        // -------------------------------------------------------------
        public async Task CargarFiltroAsync()
        {
            FiltroNombreCliente = await _servicioFacturas.ObtenerConfiguracionAsync(CLAVE_FILTRO_CLIENTE);

            var ordenacionGuardada = await _servicioFacturas.ObtenerConfiguracionAsync(CLAVE_ORDENACION);
            if (Enum.TryParse(ordenacionGuardada, out CriterioOrdenacion orden))
                OrdenacionSeleccionada = orden;
        }

        public async Task CargarFacturasAsync()
        {
            _facturasEnMemoria = (await _servicioFacturas.ObtenerTodasAsync()).ToList();
        }

        // -------------------------------------------------------------
        // FILTRADO
        // -------------------------------------------------------------
        public IEnumerable<FacturaModel> ObtenerFacturasFiltradas()
        {
            IEnumerable<FacturaModel> resultado = _facturasEnMemoria;

            if (!string.IsNullOrWhiteSpace(FiltroNombreCliente))
            {
                resultado = resultado.Where(f =>
                    f.NombreCliente.Contains(FiltroNombreCliente, StringComparison.OrdinalIgnoreCase));
            }

            resultado = OrdenacionSeleccionada switch
            {
                CriterioOrdenacion.IdDescendente => resultado.OrderByDescending(f => f.Id),
                CriterioOrdenacion.NombreClienteAscendente => resultado.OrderBy(f => f.NombreCliente),
                _ => resultado.OrderByDescending(f => f.FechaFactura)
            };

            // Guardar preferencias de usuario
            Task.Run(async () =>
            {
                await _servicioFacturas.GuardarConfiguracionAsync(CLAVE_FILTRO_CLIENTE, FiltroNombreCliente);
                await _servicioFacturas.GuardarConfiguracionAsync(CLAVE_ORDENACION, OrdenacionSeleccionada.ToString());
            });

            return resultado;
        }

        // -------------------------------------------------------------
        // CRUD FACTURAS
        // -------------------------------------------------------------
        public async Task GuardarNuevaFacturaAsync(FacturaModel nuevaFactura)
        {
            await _servicioFacturas.AgregarFacturaAsync(nuevaFactura);
            await CargarFacturasAsync();
        }

        public async Task ActualizarFacturaAsync(FacturaModel facturaEditada)
        {
            await _servicioFacturas.ActualizarFacturaAsync(facturaEditada);

            var index = _facturasEnMemoria.FindIndex(f => f.Id == facturaEditada.Id);
            if (index != -1)
                _facturasEnMemoria[index] = facturaEditada;
        }

        public async Task EliminarFacturaAsync(int facturaId)
        {
            await _servicioFacturas.EliminarFacturaAsync(facturaId);

            _facturasEnMemoria.RemoveAll(f => f.Id == facturaId);
        }

        public async Task<FacturaModel?> ObtenerFacturaPorIdAsync(int id)
        {
            var facturaEnMemoria = _facturasEnMemoria.FirstOrDefault(f => f.Id == id);
            if (facturaEnMemoria != null)
                return facturaEnMemoria;

            var facturaDesdeDb = await _servicioFacturas.ObtenerPorIdAsync(id);

            if (facturaDesdeDb != null)
                _facturasEnMemoria.Add(facturaDesdeDb);

            return facturaDesdeDb;
        }

        // -------------------------------------------------------------
        // ARCHIVAR / DESARCHIVAR (NUEVO)
        // -------------------------------------------------------------
        public async Task ArchivarFacturaAsync(int id)
        {
            await _servicioFacturas.ArchivarFacturaAsync(id);
            _facturasEnMemoria.RemoveAll(f => f.Id == id); // la quitamos de la vista normal
        }

        public async Task DesarchivarFacturaAsync(int id)
        {
            await _servicioFacturas.DesarchivarFacturaAsync(id);

            // recargar solo esta factura
            var factura = await _servicioFacturas.ObtenerPorIdAsync(id);
            if (factura != null)
                _facturasEnMemoria.Add(factura);
        }

        // -------------------------------------------------------------
        // MÉTRICAS Y ESTADÍSTICAS
        // -------------------------------------------------------------
        public Dictionary<string, decimal> ObtenerTotalPorCliente()
        {
            return _facturasEnMemoria
                .GroupBy(f => f.NombreCliente)
                .ToDictionary(g => g.Key, g => g.Sum(f => f.Total));
        }

        public Dictionary<string, int> ObtenerFrecuenciaArticulos()
        {
            return _facturasEnMemoria
                .SelectMany(f => f.Articulos)
                .GroupBy(a => a.Descripcion)
                .ToDictionary(g => g.Key, g => g.Sum(a => a.Cantidad));
        }

        public string ClienteQueMasDinero()
        {
            return _facturasEnMemoria
                .GroupBy(f => f.NombreCliente)
                .OrderByDescending(g => g.Sum(x => x.Total))
                .Select(g => g.Key)
                .FirstOrDefault() ?? "Sin datos";
        }

        public string ClienteConMasCompras()
        {
            return _facturasEnMemoria
                .GroupBy(f => f.NombreCliente)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault() ?? "Sin datos";
        }

        public string AutoMasVendido()
        {
            return _facturasEnMemoria
                .SelectMany(f => f.Articulos)
                .GroupBy(a => a.Descripcion)
                .OrderByDescending(g => g.Sum(x => x.Cantidad))
                .Select(g => g.Key)
                .FirstOrDefault() ?? "Sin datos";
        }

        public string VentaMasCara()
        {
            return _facturasEnMemoria
                .OrderByDescending(f => f.Total)
                .Select(f => $"{f.NombreCliente} (${f.Total})")
                .FirstOrDefault() ?? "Sin datos";
        }

        public string MarcaMasVendida()
        {
            var marca = _facturasEnMemoria
                .SelectMany(f => f.Articulos)
                .GroupBy(a => a.Descripcion.Split(' ')[0])
                .OrderByDescending(g => g.Sum(x => x.Cantidad))
                .FirstOrDefault();

            return marca?.Key ?? "Sin datos";
        }

        public decimal IngresoTotal()
        {
            return _facturasEnMemoria.Sum(f => f.Total);
        }

        public string ProductoMasCaro()
        {
            var prod = _facturasEnMemoria
                .SelectMany(f => f.Articulos)
                .OrderByDescending(a => a.Precio)
                .FirstOrDefault();

            return prod?.Descripcion ?? "Sin datos";
        }

        public string CategoriaMasVendida()
        {
            var categoria = _facturasEnMemoria
                .SelectMany(f => f.Articulos)
                .GroupBy(a => ObtenerCategoria(a.Descripcion))
                .OrderByDescending(g => g.Sum(x => x.Cantidad))
                .FirstOrDefault();

            return categoria?.Key ?? "Sin datos";
        }

        private string ObtenerCategoria(string desc)
        {
            if (desc.Contains("Tesla") || desc.Contains("BMW") || desc.Contains("Mercedes")) return "Autos";
            if (desc.Contains("Casa") || desc.Contains("Departamento") || desc.Contains("Terreno")) return "Bienes Raíces";
            if (desc.Contains("Gasolina")) return "Gasolina";
            if (desc.Contains("Cena") || desc.Contains("Sushi") || desc.Contains("Buffet")) return "Restaurantes";
            if (desc.Contains("Rolex") || desc.Contains("Louis Vuitton") || desc.Contains("Gucci")) return "Lujo";
            return "Otros";
        }

        public string MayorComprador()
        {
            var cliente = _facturasEnMemoria
                .GroupBy(f => f.NombreCliente)
                .OrderByDescending(g => g.Sum(f => f.Total))
                .FirstOrDefault();

            return cliente?.Key ?? "Sin datos";
        }

        public int TotalArticulosVendidos()
        {
            return _facturasEnMemoria.SelectMany(f => f.Articulos).Sum(a => a.Cantidad);
        }
    }
}
