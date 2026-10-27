$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$output = Join-Path $repo '.test-output'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$framework = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
$references = @('/reference:System.dll','/reference:System.Core.dll','/reference:System.Web.Extensions.dll','/reference:System.Xaml.dll')
$references += @('PresentationCore.dll','PresentationFramework.dll','WindowsBase.dll') | ForEach-Object { '/reference:' + (Join-Path $framework ('WPF/' + $_)) }
$suites = @{ 'QuotaTests'=@('LiveQuota.cs'); 'FollowTests'=@('CodexFollow.cs'); 'AttachmentTests'=@('WindowAttachment.cs','CodexFollow.cs','LiveQuota.cs') }
foreach ($name in $suites.Keys) {
  $sources = $suites[$name] | ForEach-Object { Join-Path $repo ('src/Orbitlet/' + $_) }
  $test = Join-Path $repo ('tests/' + $name + '.cs')
  $exe = Join-Path $output ($name + '.exe')
  & $compiler /nologo /utf8output /codepage:65001 /target:exe /warnaserror+ ('/out:' + $exe) $references $sources $test
  if ($LASTEXITCODE -ne 0) { throw ($name + ' did not compile.') }
  & $exe
  if ($LASTEXITCODE -ne 0) { throw ($name + ' failed.') }
}
