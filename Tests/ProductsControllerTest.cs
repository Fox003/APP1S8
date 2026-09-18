using Microsoft.AspNetCore.Mvc;
using Moq;
using SONDAGEAPI.Controllers;
using SONDAGEAPI.Models;
using SONDAGEAPI.Services;

namespace Tests;

public class ProductsControllerTest
{
    private Mock<IProductService> mockProductService;

    public ProductsControllerTest()
    {
        mockProductService = new Mock<IProductService>();
    }

    [Fact]
    public async Task GetProduct_ShouldReturnNotFound_WhenProductDoesNotExist()
    {
        // 1. Arrange
        int productId = 42;

        mockProductService
            .Setup(service => service.GetByIdAsync(productId))
            .ReturnsAsync((Product?)null);

        var productsController = new ProductsController(mockProductService.Object);

        // 2. Act
        var result = await productsController.GetProduct(productId);

        // 3. Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);

        Assert.Equal(404, notFoundResult.StatusCode);
        Assert.Equal($"Aucun produit avec l'id {productId}", notFoundResult.Value);
    }

    [Fact]
    public async Task GetProduct_ShouldReturnProduct_WhenProductExists()
    {
        // 1. Arrange
        var expectedProduct = new Product
        {
            Id = 1,
            Name = "Clavier mecanique",
            Price = 129.99m
        };

        mockProductService
            .Setup(service => service.GetByIdAsync(expectedProduct.Id))
            .ReturnsAsync(expectedProduct);

        var productsController = new ProductsController(mockProductService.Object);

        // 2. Act
        var result = await productsController.GetProduct(expectedProduct.Id);

        // 3. Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var product = Assert.IsType<Product>(okResult.Value);

        Assert.Equal(expectedProduct.Id, product.Id);
        Assert.Equal(expectedProduct.Name, product.Name);
        Assert.Equal(expectedProduct.Price, product.Price);
    }

    [Fact]
    public async Task GetProducts_ShouldReturnEmptyList_WhenNoProductExists()
    {
        // 1. Arrange
        mockProductService
            .Setup(service => service.GetAllAsync())
            .ReturnsAsync(new List<Product>());

        var productsController = new ProductsController(mockProductService.Object);

        // 2. Act
        var result = await productsController.GetProducts();

        // 3. Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var products = Assert.IsAssignableFrom<IEnumerable<Product>>(okResult.Value);

        Assert.Empty(products);
    }

    [Fact]
    public async Task GetProducts_ShouldReturnAllProducts_WhenProductsExist()
    {
        // 1. Arrange
        var expectedProducts = new List<Product>
        {
            new() { Id = 1, Name = "Clavier", Price = 129.99m },
            new() { Id = 2, Name = "Souris", Price = 59.50m }
        };

        mockProductService
            .Setup(service => service.GetAllAsync())
            .ReturnsAsync(expectedProducts);

        var productsController = new ProductsController(mockProductService.Object);

        // 2. Act
        var result = await productsController.GetProducts();

        // 3. Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var products = Assert.IsAssignableFrom<IEnumerable<Product>>(okResult.Value).ToList();

        Assert.Equal(2, products.Count);
        Assert.Equal("Souris", products[1].Name);
    }

    [Fact]
    public async Task GetProduct_ShouldAskTheServiceExactlyOnce()
    {
        // 1. Arrange
        int productId = 7;

        mockProductService
            .Setup(service => service.GetByIdAsync(productId))
            .ReturnsAsync(new Product { Id = productId, Name = "Ecran", Price = 349m });

        var productsController = new ProductsController(mockProductService.Object);

        // 2. Act
        await productsController.GetProduct(productId);

        // 3. Assert
        mockProductService.Verify(service => service.GetByIdAsync(productId), Times.Once);
        mockProductService.VerifyNoOtherCalls();
    }
}
