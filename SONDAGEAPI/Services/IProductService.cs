using SONDAGEAPI.Models;

namespace SONDAGEAPI.Services;

public interface IProductService
{
    Task<IEnumerable<Product>> GetAllAsync();
    Task<Product?> GetByIdAsync(int id);
}