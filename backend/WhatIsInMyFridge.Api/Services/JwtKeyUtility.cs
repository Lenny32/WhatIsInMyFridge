using System;
using System.Linq;
using System.Text;

namespace WhatIsInMyFridge.Api.Services;

internal static class JwtKeyUtility
{
    public static byte[] GetSigningKeyBytes(string rawKey)
    {
        if (string.IsNullOrWhiteSpace(rawKey))
        {
            throw new InvalidOperationException("JWT secret key is required.");
        }

        byte[] keyBytes;

        if (LooksLikeBase64(rawKey) && TryDecodeBase64(rawKey, out var decoded))
        {
            keyBytes = decoded;
        }
        else
        {
            keyBytes = Encoding.UTF8.GetBytes(rawKey);
        }

        if (keyBytes.Length < 32)
        {
            throw new InvalidOperationException(
                "JWT secret key must be at least 32 bytes. Generate one using `openssl rand -base64 48` or provide a longer plaintext value.");
        }

        return keyBytes;
    }

    private static bool LooksLikeBase64(string value)
    {
        if (value.Length % 4 != 0)
        {
            return false;
        }

        return value.All(static c =>
            char.IsLetterOrDigit(c) ||
            c == '+' ||
            c == '/' ||
            c == '=');
    }

    private static bool TryDecodeBase64(string value, out byte[] bytes)
    {
        try
        {
            bytes = Convert.FromBase64String(value);
            return true;
        }
        catch (FormatException)
        {
            bytes = Array.Empty<byte>();
            return false;
        }
    }
}
