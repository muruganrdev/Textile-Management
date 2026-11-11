using FluentValidation;
using SareeStore.Application.Common.Exceptions;
using ValidationException = SareeStore.Application.Common.Exceptions.ValidationException;
using SareeStore.Application.Common.Interfaces;
using SareeStore.Application.DTOs.Auth;
using SareeStore.Application.Services.Interfaces;
using SareeStore.Domain.Entities;
using SareeStore.Domain.Enums;

namespace SareeStore.Application.Services;

/// <summary>
/// Application service implementing authentication, customer registration, and employee onboarding.
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHashService _passwordHashService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IValidator<RegisterCustomerRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IValidator<CreateEmployeeRequest> _createEmployeeValidator;

    public AuthService(
        IUserRepository userRepository,
        IPasswordHashService passwordHashService,
        IJwtTokenGenerator jwtTokenGenerator,
        IValidator<RegisterCustomerRequest> registerValidator,
        IValidator<LoginRequest> loginValidator,
        IValidator<CreateEmployeeRequest> createEmployeeValidator)
    {
        _userRepository = userRepository;
        _passwordHashService = passwordHashService;
        _jwtTokenGenerator = jwtTokenGenerator;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _createEmployeeValidator = createEmployeeValidator;
    }

    public async Task<AuthResponse> RegisterCustomerAsync(
        RegisterCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validationResult = await _registerValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.ToDictionary());
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        if (await _userRepository.ExistsByEmailAsync(normalizedEmail, cancellationToken))
        {
            throw new ConflictException($"A user with email '{normalizedEmail}' already exists.");
        }

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = normalizedEmail,
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            Role = UserRole.Customer, // Unconditionally assign Customer role for public registration
            IsActive = true
        };

        user.PasswordHash = _passwordHashService.HashPassword(user, request.Password);

        var createdUser = await _userRepository.AddAsync(user, cancellationToken);
        var token = _jwtTokenGenerator.GenerateToken(createdUser);

        return new AuthResponse
        {
            AccessToken = token,
            ExpiresAtUtc = _jwtTokenGenerator.GetExpirationUtc(),
            User = MapToUserResponse(createdUser)
        };
    }

    public async Task<AuthResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validationResult = await _loginValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.ToDictionary());
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (user is null)
        {
            // Generic message prevents account enumeration
            throw new UnauthorizedException("Invalid email or password.");
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedException("Account has been deactivated. Please contact customer support.");
        }

        var verificationResult = _passwordHashService.VerifyPassword(user, request.Password);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            throw new UnauthorizedException("Invalid email or password.");
        }

        if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHashService.HashPassword(user, request.Password);
            await _userRepository.UpdateAsync(user, cancellationToken);
        }

        var token = _jwtTokenGenerator.GenerateToken(user);

        return new AuthResponse
        {
            AccessToken = token,
            ExpiresAtUtc = _jwtTokenGenerator.GetExpirationUtc(),
            User = MapToUserResponse(user)
        };
    }

    public async Task<UserResponse> CreateEmployeeAsync(
        CreateEmployeeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validationResult = await _createEmployeeValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.ToDictionary());
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        if (await _userRepository.ExistsByEmailAsync(normalizedEmail, cancellationToken))
        {
            throw new ConflictException($"A user with email '{normalizedEmail}' already exists.");
        }

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = normalizedEmail,
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            Role = UserRole.Employee, // Explicitly assigned Employee role
            IsActive = true
        };

        user.PasswordHash = _passwordHashService.HashPassword(user, request.Password);

        var createdUser = await _userRepository.AddAsync(user, cancellationToken);

        return MapToUserResponse(createdUser);
    }

    public async Task<UserResponse> GetCurrentUserAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedException("User account is inactive or not found.");
        }

        return MapToUserResponse(user);
    }

    private static UserResponse MapToUserResponse(User user)
    {
        return new UserResponse
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Role = user.Role.ToString(),
            IsActive = user.IsActive,
            CreatedAtUtc = user.CreatedAtUtc,
            UpdatedAtUtc = user.UpdatedAtUtc
        };
    }
}
