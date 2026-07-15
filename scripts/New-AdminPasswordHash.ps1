param([Parameter(Mandatory=$true)][string]$Password)
$iterations = 210000
$salt = New-Object byte[] 16
$rng = [Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($salt)
$derive = [Security.Cryptography.Rfc2898DeriveBytes]::new(
    $Password, $salt, $iterations, [Security.Cryptography.HashAlgorithmName]::SHA256)
$hash = $derive.GetBytes(32)
$derive.Dispose()
$rng.Dispose()
"pbkdf2`$$iterations`$$([Convert]::ToBase64String($salt))`$$([Convert]::ToBase64String($hash))"
