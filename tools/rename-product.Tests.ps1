# Pester 5+: Invoke-Pester tools
BeforeAll {
    $script:Tool = Join-Path $PSScriptRoot 'rename-product.ps1'

    function New-SampleRepository {
        # TestDrive is created and removed by Pester for every test.
        $root = Join-Path $TestDrive "repo-$([guid]::NewGuid().ToString('N'))"
        $files = @{
            'src/Alpha.Core/Alpha.Core.csproj' = '<Project><RootNamespace>Alpha.Core</RootNamespace></Project>'
            'src/Alpha.Core/AlphaSettings.cs'   = 'namespace Alpha.Core; public sealed class AlphaSettings { }'
            'README.md'                         = "# Alpha`nRepository codename-alpha; Alphabet stays.`n"
            'specs/decisions/0001-stack.md'     = 'Alpha was chosen.'
            '.gitignore'                        = 'bin/'
            'bin/Alpha.txt'                     = 'Alpha'
        }
        foreach ($entry in $files.GetEnumerator()) {
            $path = Join-Path $root $entry.Key
            New-Item -ItemType Directory -Force -Path (Split-Path $path -Parent) | Out-Null
            [IO.File]::WriteAllText($path, $entry.Value)
        }
        [IO.File]::WriteAllBytes((Join-Path $root 'src/Alpha.Core/logo.bin'), [byte[]](0x41, 0x6C, 0x70, 0x68, 0x61, 0x00))
        git -C $root init --quiet
        $root
    }
}

Describe 'rename-product' {
    BeforeEach { $script:Root = New-SampleRepository }

    It 'changes nothing on a dry run' {
        $output = & $Tool -From Alpha -To Beta -Root $Root

        $output[-1] | Should -BeLike 'Dry run:*'
        Test-Path (Join-Path $Root 'src/Alpha.Core/AlphaSettings.cs') | Should -BeTrue
        Get-Content (Join-Path $Root 'README.md') -Raw | Should -BeLike '# Alpha*'
    }

    It 'renames contents, files and folders' {
        & $Tool -From Alpha -To Beta -Root $Root -Apply | Out-Null

        Get-Content (Join-Path $Root 'src/Beta.Core/BetaSettings.cs') -Raw |
            Should -Be 'namespace Beta.Core; public sealed class BetaSettings { }'
        Get-Content (Join-Path $Root 'src/Beta.Core/Beta.Core.csproj') -Raw | Should -BeLike '*Beta.Core*'
        Test-Path (Join-Path $Root 'src/Alpha.Core') | Should -BeFalse
    }

    It 'leaves lowercase, other words, ADRs, ignored and binary files alone' {
        & $Tool -From Alpha -To Beta -Root $Root -Apply | Out-Null

        Get-Content (Join-Path $Root 'README.md') -Raw | Should -Be "# Beta`nRepository codename-alpha; Alphabet stays.`n"
        Get-Content (Join-Path $Root 'specs/decisions/0001-stack.md') -Raw | Should -Be 'Alpha was chosen.'
        Get-Content (Join-Path $Root 'bin/Alpha.txt') -Raw | Should -Be 'Alpha'
        [IO.File]::ReadAllBytes((Join-Path $Root 'src/Beta.Core/logo.bin'))[0..4] | Should -Be @(0x41, 0x6C, 0x70, 0x68, 0x61)
    }

    It 'finds nothing to do when run again' {
        & $Tool -From Alpha -To Beta -Root $Root -Apply | Out-Null

        & $Tool -From Alpha -To Beta -Root $Root -Apply | Should -BeLike 'Nothing to do*'
    }

    It 'stops before any change when a target exists' {
        New-Item -ItemType Directory -Path (Join-Path $Root 'src/Beta.Core') | Out-Null

        { & $Tool -From Alpha -To Beta -Root $Root -Apply } | Should -Throw '*already exists*'
        Get-Content (Join-Path $Root 'README.md') -Raw | Should -BeLike '# Alpha*'
    }
}
