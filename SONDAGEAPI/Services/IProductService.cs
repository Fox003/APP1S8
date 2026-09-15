using SONDAGEAPI.Models;

namespace SONDAGEAPI.Services;

public interface IProductService
{
    Task<Product?> GetByIdAsync(int id);
}