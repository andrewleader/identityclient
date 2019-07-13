using System;
using System.Collections.Generic;
using System.Text;

namespace IdentityClient
{
    /// <summary>
    /// Occurs when the token is no longer valid, user must re-login
    /// </summary>
    public class IdentityClientLostAuthorizationException : Exception
    {

    }

    /// <summary>
    /// Means should attempt again
    /// </summary>
    public class IdentityClientAccessTokenExpiredException : Exception
    {

    }

    public class IdentityClientInvalidUsernameOrPasswordException : Exception
    {

    }
}
