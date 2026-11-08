[CmdletBinding()]
param([Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$AssemblyPath)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Reflection.Metadata
$stream = [IO.File]::OpenRead([IO.Path]::GetFullPath($AssemblyPath))
$pe = $null
try {
    # Inspect PE metadata only. Do not load or execute the assembly under test.
    $pe = [System.Reflection.PortableExecutable.PEReader]::new($stream)
    $metadata = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
    $count = 0
    foreach ($handle in $metadata.GetAssemblyDefinition().GetCustomAttributes()) {
        $attribute = $metadata.GetCustomAttribute($handle)
        $constructor = $attribute.Constructor
        $type = $null
        switch ($constructor.Kind.ToString()) {
            'MethodDefinition' {
                $method = $metadata.GetMethodDefinition([System.Reflection.Metadata.MethodDefinitionHandle]$constructor)
                $type = $metadata.GetTypeDefinition($method.GetDeclaringType())
            }
            'MemberReference' {
                $method = $metadata.GetMemberReference([System.Reflection.Metadata.MemberReferenceHandle]$constructor)
                switch ($method.Parent.Kind.ToString()) {
                    'TypeReference' { $type = $metadata.GetTypeReference([System.Reflection.Metadata.TypeReferenceHandle]$method.Parent) }
                    'TypeDefinition' { $type = $metadata.GetTypeDefinition([System.Reflection.Metadata.TypeDefinitionHandle]$method.Parent) }
                    default { }
                }
            }
            default { }
        }
        if ($null -eq $type -or $metadata.GetString($type.Namespace) -ne 'StatsDirect.UI' -or
            $metadata.GetString($type.Name) -ne 'SpreadsheetGearLicenseAttribute') { continue }
        $count++
        $found = $attribute
        $signatureHandle = $method.Signature
        $constructorName = $metadata.GetString($method.Name)
    }
    if ($count -ne 1) { throw 'Expected exactly one SpreadsheetGear licence attribute.' }
    $signature = $metadata.GetBlobReader($signatureHandle)
    if ($constructorName -ne '.ctor' -or $signature.ReadByte() -ne 0x20 -or
        $signature.ReadCompressedInteger() -ne 1 -or $signature.ReadByte() -ne 0x01 -or
        $signature.ReadByte() -ne 0x0e -or $signature.RemainingBytes -ne 0) {
        throw 'Unexpected SpreadsheetGear licence attribute constructor.'
    }
    $blob = $metadata.GetBlobReader($found.Value)
    if ($blob.ReadUInt16() -ne 1) { throw 'Invalid SpreadsheetGear licence attribute metadata.' }
    if (![string]::IsNullOrEmpty($blob.ReadSerializedString())) { throw 'Compiled SpreadsheetGear licence is not empty.' }
    if ($blob.ReadUInt16() -ne 0 -or $blob.RemainingBytes -ne 0) { throw 'Unexpected SpreadsheetGear licence attribute data.' }
}
finally {
    if ($null -ne $pe) { $pe.Dispose() }
    $stream.Dispose()
}
Write-Output $true
