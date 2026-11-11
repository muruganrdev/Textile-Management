using NSubstitute;
using SareeStore.Application.Common.Exceptions;
using SareeStore.Application.Common.Interfaces;
using SareeStore.Application.DTOs.Auth;
using SareeStore.Application.Services;
using SareeStore.Application.Validators;
using SareeStore.Domain.Entities;
using SareeStore.Domain.Enums;

namespace SareeStore.UnitTests.Services;

public class AuthServiceTests
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHashService _passwordHashService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _userRepository = Substitute.For<IUserRepository>();
        _passwordHashService = Substitute.For<IPasswordHashService>();
        _jwtTokenGenerator = Substitute.For<IJwtTokenGenerator>();

        _jwtTokenGenerator.GetExpirationUtc().Returns(DateTime.UtcNow.AddHours(2));

        _authService = new AuthService(
            _userRepository,
            _passwordHashService,
            _jwtTokenGenerator,
            new RegisterCustomerRequestValidator(),
            new LoginRequestValidator(),
            new CreateEmployeeRequestValidator());
    }

    [Fact]
    public async Task RegisterCustomerAsync_WithValidRequest_ShouldCreateCustomerAndReturnAuthResponse()
    {
        // Arrange
        var request = new RegisterCustomerRequest
        {
            FullName = "Ananya Iyer",
            Email = "Ananya.Iyer@Example.com",
            Password = "SecurePassword123!",
            PhoneNumber = "+919876543210"
        };

        _userRepository.ExistsByEmailAsync("ananya.iyer@example.com", Arg.Any<CancellationToken>())
            .Returns(false);

        _passwordHashService.HashPassword(Arg.Any<User>(), request.Password)
            .Returns("hashed_secure_password_string");

        _userRepository.AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var userArg = callInfo.Arg<User>();
                userArg.Id = 10;
                return userArg;
            });

        _jwtTokenGenerator.GenerateToken(Arg.Any<User>())
            .Returns("jwt.access.token.123");

        // Act
        var response = await _authService.RegisterCustomerAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("jwt.access.token.123", response.AccessToken);
        Assert.Equal("ananya.iyer@example.com", response.User.Email);
        Assert.Equal("Ananya Iyer", response.User.FullName);
        Assert.Equal(nameof(UserRole.Customer), response.User.Role);
        Assert.True(response.User.IsActive);

        await _userRepository.Received(1).AddAsync(
            Arg.Is<User>(u =>
                u.FullName == "Ananya Iyer" &&
                u.Email == "ananya.iyer@example.com" &&
                u.Role == UserRole.Customer &&
                u.IsActive == true &&
                u.PasswordHash == "hashed_secure_password_string"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterCustomerAsync_WithDuplicateEmail_ShouldThrowConflictException()
    {
        // Arrange
        var request = new RegisterCustomerRequest
        {
            FullName = "Test Duplicate",
            Email = "duplicate@example.com",
            Password = "StrongPassword#1"
        };

        _userRepository.ExistsByEmailAsync("duplicate@example.com", Arg.Any<CancellationToken>())
            .Returns(true);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(() => _authService.RegisterCustomerAsync(request));
        Assert.Contains("duplicate@example.com", ex.Message);

        await _userRepository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        _passwordHashService.DidNotReceive().HashPassword(Arg.Any<User>(), Arg.Any<string>());
    }

    [Fact]
    public async Task RegisterCustomerAsync_WithInvalidPassword_ShouldThrowValidationException()
    {
        // Arrange
        var request = new RegisterCustomerRequest
        {
            FullName = "Invalid Password User",
            Email = "test@example.com",
            Password = "weak" // too short, no uppercase, no digit, no special char
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _authService.RegisterCustomerAsync(request));
        await _userRepository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ShouldReturnAuthResponse()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "Meera.Nair@Example.com",
            Password = "Password123!"
        };

        var user = new User
        {
            Id = 5,
            FullName = "Meera Nair",
            Email = "meera.nair@example.com",
            PasswordHash = "stored_valid_hash",
            Role = UserRole.Customer,
            IsActive = true
        };

        _userRepository.GetByEmailAsync("meera.nair@example.com", Arg.Any<CancellationToken>())
            .Returns(user);

        _passwordHashService.VerifyPassword(user, request.Password)
            .Returns(PasswordVerificationResult.Success);

        _jwtTokenGenerator.GenerateToken(user)
            .Returns("jwt.meera.token");

        // Act
        var response = await _authService.LoginAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("jwt.meera.token", response.AccessToken);
        Assert.Equal("meera.nair@example.com", response.User.Email);
        Assert.Equal(nameof(UserRole.Customer), response.User.Role);
    }

    [Fact]
    public async Task LoginAsync_WithNonExistentEmail_ShouldThrowUnauthorizedExceptionWithGenericMessage()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "nonexistent@example.com",
            Password = "Password123!"
        };

        _userRepository.GetByEmailAsync("nonexistent@example.com", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => _authService.LoginAsync(request));
        Assert.Equal("Invalid email or password.", ex.Message);
    }

    [Fact]
    public async Task LoginAsync_WithIncorrectPassword_ShouldThrowUnauthorizedExceptionWithGenericMessage()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "user@example.com",
            Password = "WrongPassword999!"
        };

        var user = new User
        {
            Id = 1,
            FullName = "Active User",
            Email = "user@example.com",
            PasswordHash = "stored_hash",
            IsActive = true
        };

        _userRepository.GetByEmailAsync("user@example.com", Arg.Any<CancellationToken>())
            .Returns(user);

        _passwordHashService.VerifyPassword(user, request.Password)
            .Returns(PasswordVerificationResult.Failed);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => _authService.LoginAsync(request));
        Assert.Equal("Invalid email or password.", ex.Message);
        _jwtTokenGenerator.DidNotReceive().GenerateToken(Arg.Any<User>());
    }

    [Fact]
    public async Task LoginAsync_WithInactiveAccount_ShouldThrowUnauthorizedException()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "inactive@example.com",
            Password = "Password123!"
        };

        var inactiveUser = new User
        {
            Id = 8,
            FullName = "Deactivated User",
            Email = "inactive@example.com",
            PasswordHash = "stored_hash",
            IsActive = false
        };

        _userRepository.GetByEmailAsync("inactive@example.com", Arg.Any<CancellationToken>())
            .Returns(inactiveUser);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => _authService.LoginAsync(request));
        Assert.Contains("deactivated", ex.Message, StringComparison.OrdinalIgnoreCase);
        _passwordHashService.DidNotReceive().VerifyPassword(Arg.Any<User>(), Arg.Any<string>());
    }

    [Fact]
    public async Task LoginAsync_WhenRehashNeeded_ShouldUpdateStoredPasswordHash()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "rehash@example.com",
            Password = "OldPassword123!"
        };

        var user = new User
        {
            Id = 12,
            FullName = "Rehash User",
            Email = "rehash@example.com",
            PasswordHash = "old_legacy_hash",
            IsActive = true
        };

        _userRepository.GetByEmailAsync("rehash@example.com", Arg.Any<CancellationToken>())
            .Returns(user);

        _passwordHashService.VerifyPassword(user, request.Password)
            .Returns(PasswordVerificationResult.SuccessRehashNeeded);

        _passwordHashService.HashPassword(user, request.Password)
            .Returns("upgraded_new_hash_string");

        _jwtTokenGenerator.GenerateToken(user)
            .Returns("jwt.rehashed.token");

        // Act
        var response = await _authService.LoginAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("jwt.rehashed.token", response.AccessToken);
        Assert.Equal("upgraded_new_hash_string", user.PasswordHash);

        await _userRepository.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateEmployeeAsync_WithValidRequest_ShouldAssignEmployeeRoleAndReturnUserResponse()
    {
        // Arrange
        var request = new CreateEmployeeRequest
        {
            FullName = "Rajesh Sharma",
            Email = "Rajesh.Staff@SareeStore.com",
            Password = "StaffPassword2026!",
            PhoneNumber = "+919876543211"
        };

        _userRepository.ExistsByEmailAsync("rajesh.staff@sareestore.com", Arg.Any<CancellationToken>())
            .Returns(false);

        _passwordHashService.HashPassword(Arg.Any<User>(), request.Password)
            .Returns("hashed_staff_password");

        _userRepository.AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var userArg = callInfo.Arg<User>();
                userArg.Id = 25;
                return userArg;
            });

        // Act
        var response = await _authService.CreateEmployeeAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(25, response.Id);
        Assert.Equal("rajesh.staff@sareestore.com", response.Email);
        Assert.Equal("Rajesh Sharma", response.FullName);
        Assert.Equal(nameof(UserRole.Employee), response.Role);
        Assert.True(response.IsActive);

        await _userRepository.Received(1).AddAsync(
            Arg.Is<User>(u =>
                u.FullName == "Rajesh Sharma" &&
                u.Email == "rajesh.staff@sareestore.com" &&
                u.Role == UserRole.Employee &&
                u.IsActive == true &&
                u.PasswordHash == "hashed_staff_password"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateEmployeeAsync_WithDuplicateEmail_ShouldThrowConflictException()
    {
        // Arrange
        var request = new CreateEmployeeRequest
        {
            FullName = "Duplicate Staff",
            Email = "staff@sareestore.com",
            Password = "StaffPassword2026!"
        };

        _userRepository.ExistsByEmailAsync("staff@sareestore.com", Arg.Any<CancellationToken>())
            .Returns(true);

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() => _authService.CreateEmployeeAsync(request));
        await _userRepository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetCurrentUserAsync_WithActiveUser_ShouldReturnUserResponse()
    {
        // Arrange
        var user = new User
        {
            Id = 42,
            FullName = "Pooja Hegde",
            Email = "pooja@example.com",
            Role = UserRole.Customer,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _userRepository.GetByIdAsync(42, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        var response = await _authService.GetCurrentUserAsync(42);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(42, response.Id);
        Assert.Equal("pooja@example.com", response.Email);
        Assert.Equal("Pooja Hegde", response.FullName);
        Assert.Equal(nameof(UserRole.Customer), response.Role);
        Assert.True(response.IsActive);
    }

    [Fact]
    public async Task GetCurrentUserAsync_WithNonExistentUser_ShouldThrowUnauthorizedException()
    {
        // Arrange
        _userRepository.GetByIdAsync(999, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => _authService.GetCurrentUserAsync(999));
        Assert.Contains("inactive or not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetCurrentUserAsync_WithInactiveUser_ShouldThrowUnauthorizedException()
    {
        // Arrange
        var inactiveUser = new User
        {
            Id = 55,
            FullName = "Deactivated User",
            Email = "inactive@example.com",
            Role = UserRole.Customer,
            IsActive = false
        };

        _userRepository.GetByIdAsync(55, Arg.Any<CancellationToken>())
            .Returns(inactiveUser);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => _authService.GetCurrentUserAsync(55));
        Assert.Contains("inactive or not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
