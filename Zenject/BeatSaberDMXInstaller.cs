//  Beat Saber Custom Avatars - Custom player models for body presence in Beat Saber.
//  Copyright © 2018-2021  Nicolas Gnyra and Beat Saber Custom Avatars Contributors
//
//  This library is free software: you can redistribute it and/or
//  modify it under the terms of the GNU Lesser General Public
//  License as published by the Free Software Foundation, either
//  version 3 of the License, or (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU Lesser General Public License for more details.
//
//  You should have received a copy of the GNU Lesser General Public License
//  along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System;
using BeatSaberDMX.Logging;
using BeatSaberDMX.Utilities;
using IPA.Utilities;
using Zenject;
using Logger = IPA.Logging.Logger;

namespace BeatSaberDMX.Zenject
{
    internal class BeatSaberDMXInstaller : Installer
    {
        public static readonly int kPlayerAvatarManagerExecutionOrder = 1000;

        private readonly ILogger<BeatSaberDMXInstaller> _logger;
        private readonly Logger _ipaLogger;
        private readonly PCAppInit _pcAppInit;

        public BeatSaberDMXInstaller(Logger ipaLogger, PCAppInit pcAppInit)
        {
            _logger = new IPALogger<BeatSaberDMXInstaller>(ipaLogger);
            _ipaLogger = ipaLogger;
            _pcAppInit = pcAppInit;
        }

        public override void InstallBindings()
        {
            // logging
            Container.Bind(typeof(ILogger<>)).FromMethodUntyped(CreateLogger).AsTransient();

            Container.BindInterfacesAndSelfTo<BeatSaberUtilities>().AsSingle();

            // helper classes
            Container.Bind<TrackingHelper>().AsTransient();

            Container.Bind<MainSettingsModelSO>().FromInstance(_pcAppInit.GetField<MainSettingsModelSO, PCAppInit>("_mainSettingsModel")).IfNotBound();
        }

        private object CreateLogger(InjectContext context)
        {
            Type genericType = context.MemberType.GenericTypeArguments[0];

            return genericType.IsAssignableFrom(context.ObjectType)
                ? Activator.CreateInstance(typeof(IPALogger<>).MakeGenericType(genericType), _ipaLogger)
                : throw new InvalidOperationException($"Cannot create logger with generic type '{genericType}' for type '{context.ObjectType}'");
        }
    }
}