using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SONDAGEAPI.Services;

namespace SONDAGEAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductsController(IProductService productService) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetProduct(int id)
    {
        var product = await productService.GetByIdAsync(id);

        if (product is null)
            return NotFound($"Aucun produit avec l'id {id}");

        return Ok(product);
    }
    
    [HttpGet]
    public async Task<IActionResult> GetProducts()
    {
        var products = await productService.GetAllAsync();
        return Ok(products);
    }
}