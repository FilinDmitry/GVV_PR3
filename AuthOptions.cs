using Microsoft.IdentityModel.Tokens;
using System.Text;
namespace GVV_PR3


{
    static public class AuthOptions
    {
        public const string ISSUER = "MainServer";
        public const string AUDIENCE = "Client";
        const string KEY = "Valeraprishelinaslediltutsvoimvaibcodom";
        public static SymmetricSecurityKey GetSymmetricSecurityKey() =>
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(KEY));
    }
}


