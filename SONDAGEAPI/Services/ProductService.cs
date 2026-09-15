using SONDAGEAPI.Data;
using SONDAGEAPI.Models;

namespace SONDAGEAPI.Services;

public class ProductService(ApplicationDbContext db) : IProductService
{
    public async Task<Product?> GetByIdAsync(int id)
    {
        return await db.Products.FindAsync(id);
    }
}