# Unity Utility Packages

This repository contains a collection of Unity packages developed by Meta, designed to enhance and streamline various aspects of Unity development. Below is a table summarizing each package included in this repository, along with a brief description of its functionality.

## General Unity Utilities

These packages don't require the Meta XR Core SDK, and may be helpful for any Unity project.

| Name | Package Name | Description |
|-|-|-|
| [General Utilities](./com.meta.utilities/) | `com.meta.utilities` | General utilities for Unity development, including singleton patterns, extension methods, and build tools. |
| [Narrative System](./com.meta.utilities.narrative/) | `com.meta.utilities.narrative` | Complete narrative system for driving gameplay and interaction, used in the North Star graphical showcase. |
| [Viewport Renderer](./com.meta.utilities.viewport-renderer/) | `com.meta.utilities.viewport-renderer` | Provides functionality to efficiently render a stencilled view of the game world, acting as a portal. |
| [Real-time Watch Window](./com.meta.utilities.watch-window/) | `com.meta.utilities.watch-window` | Adds a "Watch Window" to the Unity Editor for quick inspection and analysis of C# expressions in real-time. |

## XR Utilities

These packages are helpful for XR projects, particularly those using Meta SDKs.

| Name | Package Name | Description |
|-|-|-|
| [Input Utilities](./com.meta.utilities.input/) | `com.meta.utilities.input` | Utilities related to Unity's Input System, including XR Toolkit integration and XR Device FPS Simulator. |
| [Avatar Utilities](./com.meta.utilities.avatars/) | `com.meta.utilities.avatars` | Implementation of the AvatarEntity class for integrating Meta Avatars SDK into networked Unity projects. |
| [Environment System](./com.meta.utilities.environment/) | `com.meta.utilities.environment` | Environment system used in the North Star graphics showcase, featuring clouds, weather systems, and a dynamically simulated ocean. |
| [Rope System](./com.meta.utilities.ropes/) | `com.meta.utilities.ropes` | Rope simulation system with burst-job based verlet rope and anchoring system for interaction. |
| [Multiplayer Utilities for Netcode and Photon](./com.meta.multiplayer.netcode-photon/) | `com.meta.multiplayer.netcode-photon` | Core implementation for starting a multiplayer project using Netcode for GameObjects and Photon as the transport layer. Includes avatar and core networking utilities. |
| [Tutorials Framework Hub](./com.meta.tutorial.framework/) | `com.meta.tutorial.framework` | Framework for generating the in-Unity tutorials used in our samples and showcases. |

## Installation

To integrate any of these packages into your Unity project, use the Package Manager to [add the respective Git URL](https://docs.unity3d.com/Manual/upm-ui-giturl.html) provided in each package's README file. For example:

```txt
https://github.com/meta-quest/Unity-UtilityPackages.git?path=com.meta.utilities
```

## Contributing

Contributions are welcome! Please read the [contributing guidelines](./CONTRIBUTING.md) for more information.

## License

This project is licensed under the MIT License - see the [LICENSE](./LICENSE) file for details.

## Contact

For any questions or issues, please contact the repository maintainers or open an issue on GitHub.
