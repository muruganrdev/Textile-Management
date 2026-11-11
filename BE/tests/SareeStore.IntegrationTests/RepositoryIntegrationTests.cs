using Microsoft.EntityFrameworkCore;
using SareeStore.Domain.Entities;
using SareeStore.Domain.Enums;
using SareeStore.Infrastructure.Repositories;

namespace SareeStore.IntegrationTests;

[Collection("DatabaseTests")]
public class RepositoryIntegrationTests
{
    [Fact]
    public async Task ProductRepository_CrudAndQueryOperations_ShouldWorkCorrectly()
    {
        await using var context = TestDbContextFactory.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        try
        {
            var categoryRepo = new CategoryRepository(context);
            var productRepo = new ProductRepository(context);

            // 1. Seed Category
            var category = new Category
            {
                Name = $"Test Silk {Guid.NewGuid():N}",
                Slug = $"test-silk-{Guid.NewGuid():N}",
                IsActive = true
            };
            await categoryRepo.AddAsync(category);

            // 2. Seed Product with Inventory and Image
            var uniqueSku = $"TEST-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
            var uniqueSlug = $"test-saree-{Guid.NewGuid():N}";
            var product = new Product
            {
                CategoryId = category.Id,
                SKU = uniqueSku,
                Title = "Pure Kanchipuram Gold Zari Saree",
                Slug = uniqueSlug,
                Description = "Handcrafted test saree",
                Price = 18500m,
                DiscountPrice = 16500m,
                Fabric = "Mulberry Silk",
                WeaveType = "Handloom Korvai",
                ZariType = "Pure Gold Zari",
                PrimaryColor = "Maroon",
                SareeLengthMeters = 5.5m,
                SareeWidthInches = 48.0m,
                BlouseIncluded = true,
                BlouseLengthMeters = 0.8m,
                IsActive = true
            };

            product.Images.Add(new ProductImage
            {
                ImageUrl = "https://cdn.example.com/test-img1.jpg",
                DisplayOrder = 1,
                IsPrimary = true
            });

            product.Inventory = new Inventory
            {
                StockQuantity = 5,
                LowStockThreshold = 2
            };

            var added = await productRepo.AddAsync(product);
            Assert.True(added.Id > 0);

            // 3. Test Product Retrieval & Relationship Loading
            var fetched = await productRepo.GetByIdAsync(added.Id);
            Assert.NotNull(fetched);
            Assert.NotNull(fetched.Category);
            Assert.Equal(category.Name, fetched.Category.Name);
            Assert.NotNull(fetched.Inventory);
            Assert.Equal(5, fetched.Inventory.StockQuantity);
            Assert.NotEmpty(fetched.Images);
            Assert.True(fetched.Images.First().IsPrimary);

            // 4. Test Category Filtering
            var categoryProducts = await productRepo.GetByCategoryIdAsync(category.Id);
            Assert.Contains(categoryProducts, p => p.Id == added.Id);

            // 5. Test SKU and Slug duplicate checks
            var skuExists = await productRepo.ExistsBySkuAsync(uniqueSku);
            Assert.True(skuExists);

            var skuExcludedExists = await productRepo.ExistsBySkuAsync(uniqueSku, added.Id);
            Assert.False(skuExcludedExists);

            var slugExists = await productRepo.ExistsBySlugAsync(uniqueSlug);
            Assert.True(slugExists);

            var slugExcludedExists = await productRepo.ExistsBySlugAsync(uniqueSlug, added.Id);
            Assert.False(slugExcludedExists);

            // 6. Test Historical Orders Check (Should be false before any order)
            var hasOrdersBefore = await productRepo.HasOrdersAsync(added.Id);
            Assert.False(hasOrdersBefore);

            // 7. Attach an Order to test historical order detection
            var user = new User
            {
                FullName = "Test Customer",
                Email = $"test-customer-{Guid.NewGuid():N}@example.com",
                PasswordHash = "hashed-pw",
                Role = UserRole.Customer,
                IsActive = true
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var order = new Order
            {
                OrderNumber = $"ORD-TEST-{Guid.NewGuid():N}",
                CustomerId = user.Id,
                SubTotal = 18500m,
                TotalAmount = 18500m,
                OrderStatus = OrderStatus.Placed,
                PaymentStatus = PaymentStatus.Pending,
                ShippingRecipientName = "Test Customer",
                ShippingPhoneNumber = "9876543210",
                ShippingAddressLine1 = "123 Test St",
                ShippingCity = "Chennai",
                ShippingState = "Tamil Nadu",
                ShippingPostalCode = "600001"
            };
            context.Orders.Add(order);
            await context.SaveChangesAsync();

            var orderItem = new OrderItem
            {
                OrderId = order.Id,
                ProductId = added.Id,
                ProductTitleSnapshot = product.Title,
                ProductSkuSnapshot = product.SKU,
                RegularPrice = 18500m,
                UnitPrice = 18500m,
                DiscountAmount = 0m,
                Quantity = 1,
                LineTotal = 18500m
            };
            context.OrderItems.Add(orderItem);
            await context.SaveChangesAsync();

            var hasOrdersAfter = await productRepo.HasOrdersAsync(added.Id);
            Assert.True(hasOrdersAfter);
        }
        finally
        {
            await transaction.RollbackAsync();
        }
    }

    [Fact]
    public async Task CategoryAndCollectionRepository_Operations_ShouldWorkCorrectly()
    {
        await using var context = TestDbContextFactory.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        try
        {
            var categoryRepo = new CategoryRepository(context);
            var collectionRepo = new CollectionRepository(context);

            // Test Category CRUD
            var uniqueCatName = $"Banarasi Silk {Guid.NewGuid():N}";
            var uniqueCatSlug = $"banarasi-silk-{Guid.NewGuid():N}";
            var category = new Category
            {
                Name = uniqueCatName,
                Slug = uniqueCatSlug,
                Description = "Handwoven Banarasi sarees",
                IsActive = true
            };

            await categoryRepo.AddAsync(category);
            Assert.True(category.Id > 0);

            var fetchedCat = await categoryRepo.GetBySlugAsync(uniqueCatSlug);
            Assert.NotNull(fetchedCat);
            Assert.Equal(uniqueCatName, fetchedCat.Name);

            var catNameExists = await categoryRepo.ExistsByNameAsync(uniqueCatName);
            Assert.True(catNameExists);

            var countBefore = await categoryRepo.GetProductCountAsync(category.Id);
            Assert.Equal(0, countBefore);

            var allCounts = await categoryRepo.GetProductCountsAsync();
            Assert.NotNull(allCounts);

            // Test Collection CRUD
            var uniqueCollName = $"Heritage Bridal {Guid.NewGuid():N}";
            var uniqueCollSlug = $"heritage-bridal-{Guid.NewGuid():N}";
            var collection = new Collection
            {
                Name = uniqueCollName,
                Slug = uniqueCollSlug,
                Description = "Curated wedding collection",
                IsFeatured = true,
                IsActive = true
            };

            await collectionRepo.AddAsync(collection);
            Assert.True(collection.Id > 0);

            var fetchedColl = await collectionRepo.GetBySlugAsync(uniqueCollSlug);
            Assert.NotNull(fetchedColl);
            Assert.Equal(uniqueCollName, fetchedColl.Name);
            Assert.True(fetchedColl.IsFeatured);

            var allColls = await collectionRepo.GetAllAsync();
            Assert.Contains(allColls, c => c.Id == collection.Id);
        }
        finally
        {
            await transaction.RollbackAsync();
        }
    }

    [Fact]
    public async Task UserRepository_CrudAndQueryOperations_ShouldWorkCorrectly()
    {
        await using var context = TestDbContextFactory.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        try
        {
            var userRepo = new UserRepository(context);

            var uniqueEmail = $"repo_user_{Guid.NewGuid():N}@example.com";
            var user = new User
            {
                FullName = "Meenakshi Sundaram",
                Email = uniqueEmail,
                PasswordHash = "initial_hash_12345",
                PhoneNumber = "+919876543210",
                Role = UserRole.Customer,
                IsActive = true
            };

            // 1. AddAsync
            await userRepo.AddAsync(user);
            Assert.True(user.Id > 0);

            // 2. GetByIdAsync
            var fetchedById = await userRepo.GetByIdAsync(user.Id);
            Assert.NotNull(fetchedById);
            Assert.Equal(user.Email.ToLowerInvariant(), fetchedById.Email);

            // 3. GetByEmailAsync (test normalization)
            var fetchedByEmail = await userRepo.GetByEmailAsync(uniqueEmail.ToUpperInvariant());
            Assert.NotNull(fetchedByEmail);
            Assert.Equal(user.Id, fetchedByEmail.Id);

            // 4. ExistsByEmailAsync
            var exists = await userRepo.ExistsByEmailAsync($"  {uniqueEmail}  ");
            Assert.True(exists);

            var nonExistent = await userRepo.ExistsByEmailAsync("doesnotexist@example.com");
            Assert.False(nonExistent);

            // 5. UpdateAsync
            user.PasswordHash = "upgraded_hash_56789";
            user.PhoneNumber = "+919876543999";
            await userRepo.UpdateAsync(user);

            var updatedUser = await userRepo.GetByIdAsync(user.Id);
            Assert.NotNull(updatedUser);
            Assert.Equal("upgraded_hash_56789", updatedUser.PasswordHash);
            Assert.Equal("+919876543999", updatedUser.PhoneNumber);

            // 6. GetEmployeesAsync
            var employeeEmail = $"staff_{Guid.NewGuid():N}@sareestore.com";
            var employee = new User
            {
                FullName = "Ramesh Kumar Staff",
                Email = employeeEmail,
                PasswordHash = "staff_hash_123",
                Role = UserRole.Employee,
                IsActive = true
            };
            await userRepo.AddAsync(employee);

            var employees = await userRepo.GetEmployeesAsync();
            Assert.Contains(employees, e => e.Id == employee.Id && e.Role == UserRole.Employee);
            Assert.DoesNotContain(employees, e => e.Id == user.Id); // Customer not in employee list
        }
        finally
        {
            await transaction.RollbackAsync();
        }
    }
}
