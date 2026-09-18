using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using SONDAGEAPI.Data;
using SONDAGEAPI.Models;

namespace SONDAGEAPI.Services;

[ExcludeFromCodeCoverage]
public class ProductService(ApplicationDbContext db) : IProductService
{
    public async Task<IEnumerable<Product>> GetAllAsync()
    {
        return await db.Products.ToListAsync();
    }
    
    public async Task<Product?> GetByIdAsync(int id)
    {
        return await db.Products.FindAsync(id);
    }
}