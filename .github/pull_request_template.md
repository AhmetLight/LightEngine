## Description
Please include a summary of the changes and the related issue. Please also include relevant motivation and context.

Fixes # (issue)

## Type of Change
- [ ] Bug fix (non-breaking change which fixes an issue)
- [ ] New feature (non-breaking change which adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality to not work as expected)
- [ ] Documentation update
- [ ] Performance optimization

## Architecture & Code Quality Checklist:
- [ ] My code follows the architectural guidelines of Light Engine (Service Locator, Zero-GC allocation pools, XML docs).
- [ ] I have verified that `dotnet build -c Release` compiles with 0 errors and 0 warnings.
- [ ] All 9 test stages pass cleanly: `dotnet run --project LightEngine.Tests -c Release`.
- [ ] I have updated documentation or XML comments if applicable.
- [ ] I have tested changes on both Scene Viewport and Game Viewport runtime modes.
