using System.ComponentModel.DataAnnotations;
using Terminkalender.Models;
using System.ComponentModel.DataAnnotations;
using Terminkalender.Validators;

namespace Terminkalender.Models
{
    public class Reservation
    {
        public int Id { get; set; }
        [Required(ErrorMessage = "Das datum ist erforderlich.")]
        [FutureDate(ErrorMessage = "Das Datum muss in der Zukunft liegen")]
        public DateTime Date { get; set; }

        public TimeOnly StartTime { get; set; }

        public TimeOnly EndTime { get; set; }

        public Room Room { get; set; }

        public string Organizer { get; set; }
        [Required(ErrorMessage ="Beschreibung ist erforderlich.")]
        [StringLength(200, MinimumLength = 10, ErrorMessage = "Beschreibung muss zwischen 10 und 200 Zeichen lang sein")]
        [RegularExpression(@"^[a-zA-Z0-9\s]*$", ErrorMessage = "Beschreibung darf nur alphanumerische Zeichen beinhalten")]
        public string Remarks { get; set; }

        public Guid PrivateKey { get; set; }

        public Guid PublicKey { get; set; }
        [Required(ErrorMessage = "Die Teilnehmerliste darf nicht leer sein.")]
        [RegularExpression(@"^([A-Za-z\s]+,)*[A-Za-z\s]+$", ErrorMessage = "Die Teilnehmerliste darf nur Buchstaben und Kommas enthalten.")]
        public string Participants { get; set; }
    }
}
