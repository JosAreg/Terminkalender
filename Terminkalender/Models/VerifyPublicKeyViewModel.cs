using System.ComponentModel.DataAnnotations;


namespace Terminkalender.Models
{
    public class VerifyPublicKeyViewModel
    {
        [Required(ErrorMessage = "PublicKey ist erforderlich.")]
        public Guid PublicKey { get; set; }

        [Required]
        public int ReservationId { get; set; }
        public string ReturnAction { get; set; }
    }
}
