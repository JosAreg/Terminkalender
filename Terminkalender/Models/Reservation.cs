using System.ComponentModel.DataAnnotations;
using Terminkalender.Validators;

namespace Terminkalender.Models
{
    public class Reservation
    {
        public int Id { get; set; }
        [Required(ErrorMessage = "Das Datum ist erforderlich.")]
        [FutureDate(ErrorMessage = "Das Datum muss in der Zukunft liegen")]
        [DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:yyyy-MM-dd}")]
        public DateTime Date { get; set; }
        [Required(ErrorMessage = "Die Startzeit ist erforderlich")]
        public TimeOnly StartTime { get; set; }
        [Required(ErrorMessage = "Die Endzeit ist erforderlich")]
        public TimeOnly EndTime { get; set; }
        [Required(ErrorMessage = "Wählen Sie einen Raum")]
        public Room Room { get; set; }
        [Required(ErrorMessage = "der Organisator ist erforderlich")]
        public string Organizer { get; set; }
        [Required(ErrorMessage = "Beschreibung ist erforderlich.")]
        [StringLength(200, MinimumLength = 10, ErrorMessage = "Beschreibung muss zwischen 10 und 200 Zeichen lang sein")]
        [RegularExpression(@"^[a-zA-Z0-9\s,.:-]*$", ErrorMessage = @"Ungültige Zeichen. Erlaubt sind Buchstaben, Zahlen, Leerzeichen, Zeilenumbrüche sowie :,-,!() und die angegebenen Sonderzeichen.")]
        public string Remarks { get; set; }
        public Guid PrivateKey { get; set; }
        public Guid PublicKey { get; set; }
        [Required(ErrorMessage = "Die Teilnehmerliste darf nicht leer sein.")]
        [RegularExpression(@"^[a-zA-Z0-9\s,.:-]*$", ErrorMessage = @"Ungültige Zeichen. Erlaubt sind Buchstaben, Zahlen, Leerzeichen, Zeilenumbrüche sowie :,-,!() und die angegebenen Sonderzeichen.")]
        public string Participants { get; set; }
    }
}
