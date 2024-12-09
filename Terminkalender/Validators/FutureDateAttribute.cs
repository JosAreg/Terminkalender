using System.ComponentModel.DataAnnotations;

namespace Terminkalender.Validators
{
    public class FutureDateAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is DateTime dateValue)
            {
                if (dateValue.Date > DateTime.Now.Date)
                {
                    return ValidationResult.Success;
                }
                else
                {
                    return new ValidationResult(ErrorMessage ?? "Das Datum muss in der Zukunft liegen, aussder du kannst Zeitreisen");
                }
            }
            return new ValidationResult("Ungültiges Datum");
        }
    }
}
