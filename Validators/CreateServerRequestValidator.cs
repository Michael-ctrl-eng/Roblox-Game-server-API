using FluentValidation;
using RobloxGameServerAPI.Models;
using System.Text.RegularExpressions; // Add Regex namespace

namespace RobloxGameServerAPI.Validators
{
    public class CreateServerRequestValidator : AbstractValidator<CreateServerRequest>
    {
        public CreateServerRequestValidator()
        {
            RuleFor(request => request.Name)
                .NotEmpty().WithMessage("Server Name is required.")
                .MaximumLength(255).WithMessage("Server Name cannot exceed 255 characters.")
                .Matches(@"^[a-zA-Z0-9\s_-]+$").WithMessage("Server Name can only contain letters, numbers, spaces, underscores, and hyphens.")
                .Must(BeSafeString).WithMessage("Server Name contains potentially unsafe characters."); 

            RuleFor(request => request.RobloxPlaceID)
                .GreaterThan(0).WithMessage("Roblox Place ID must be a positive number.");

            RuleFor(request => request.MaxPlayers)
                .InclusiveBetween(1, 1000).WithMessage("Max Players must be between 1 and 1000.");

            RuleFor(request => request.Region)
                .NotEmpty().WithMessage("Region is required.")
                .MaximumLength(50).WithMessage("Region cannot exceed 50 characters.")
                .Matches(@"^[a-zA-Z0-9\s]+$").WithMessage("Region can only contain letters, numbers, and spaces.");

            RuleFor(request => request.GameMode)
                .NotEmpty().WithMessage("Game Mode is required.")
                .MaximumLength(100).WithMessage("Game Mode cannot exceed 100 characters.")
                .Matches(@"^[a-zA-Z0-9\s_-]+$").WithMessage("Game Mode can only contain letters, numbers, spaces, underscores, and hyphens.");

            // Example - Cross-Field Validation - Max Players vs. Game Mode (Conceptual - adapt to your game logic)
            RuleFor(request => request)
                .Must(request => BeValidPlayerCountForGameMode(request.MaxPlayers, request.GameMode))
                .WithMessage("Max Players is not valid for the selected Game Mode.");
        }

        // Example - Custom Sanitization Rule (replace with more robust sanitization logic for real-world)
        private bool BeSafeString(string value)
        {
            if (string.IsNullOrEmpty(value)) return true; // Allow empty strings if needed
            // Example - Blacklist approach (not always best, but illustrative)
            string blacklistRegex = @"[<>{}#%&*;:'""[\]/\\`~]"; // Example - Characters to blacklist - adjust as needed
            return !Regex.IsMatch(value, blacklistRegex);
        }

        private bool BeValidPlayerCountForGameMode(int maxPlayers, string gameMode)
        {
            if (string.IsNullOrEmpty(gameMode)) return true; // Allow if game mode is not specified

            gameMode = gameMode.ToLowerInvariant();
            if (gameMode == "ffa" && maxPlayers > 50) // Example: FFA game mode limit
            {
                return false;
            }
            if (gameMode == "tdm" && maxPlayers < 2) // Example: TDM minimum players
            {
                return false;
            }
            return true; // Default valid
        }
    }
}
