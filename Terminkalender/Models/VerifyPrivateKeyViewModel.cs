using System.ComponentModel.DataAnnotations;
using Terminkalender.Models;
using System.ComponentModel.DataAnnotations;
using Terminkalender.Validators;


namespace Terminkalender.Models
{
    public class VerifyPrivateKeyViewModel
    {
        [Required(ErrorMessage = "Private Key ist erforderlich.")]
        public Guid PrivateKey { get; set; }

        [Required]
        public int ReservationId { get; set; }
        public string ReturnAction { get; set; }
    }
}
