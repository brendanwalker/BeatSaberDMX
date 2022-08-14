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

using UnityEngine;

namespace BeatSaberDMX.Utilities
{
    public class TrackingHelper
    {
        private readonly BeatSaberUtilities _beatSaberUtilities;

        internal TrackingHelper(BeatSaberUtilities beatSaberUtilities)
        {
            _beatSaberUtilities = beatSaberUtilities;
        }

        public void SetLocalPose(Vector3 position, Quaternion rotation, Transform target, Transform parent = null)
        {
            ApplyLocalPose(ref position, ref rotation, parent);

            target.SetPositionAndRotation(position, rotation);
        }

        public void ApplyLocalPose(ref Vector3 position, ref Quaternion rotation, Transform parent = null)
        {
            // if avatar transform has a parent, use that as the origin
            // this is like localPosition/localRotation but on the parent object
            if (parent)
            {
                position = parent.TransformPoint(position);
                rotation = parent.rotation * rotation;
            }
        }

        /// <summary>
        /// Applies the game's room adjustment to the specified position and rotation.
        /// For the inverse, see of <see cref="ApplyInverseRoomAdjust(ref Vector3, ref Quaternion)"/>.
        /// </summary>
        /// <param name="position">Position to adjust.</param>
        /// <param name="rotation">Rotation to adjust.</param>
        public void ApplyRoomAdjust(ref Vector3 position, ref Quaternion rotation)
        {
            Vector3 roomCenter = _beatSaberUtilities.roomCenter;
            Quaternion roomRotation = _beatSaberUtilities.roomRotation;

            position = roomCenter + roomRotation * position;
            rotation = roomRotation * rotation;
        }

        /// <summary>
        /// Applies the inverse of the game's room adjustment to the specified position and rotation.
        /// Inverse of <see cref="ApplyRoomAdjust(ref Vector3, ref Quaternion)"/>.
        /// </summary>
        /// <param name="position">Position to adjust.</param>
        /// <param name="rotation">Rotation to adjust.</param>
        public void ApplyInverseRoomAdjust(ref Vector3 position, ref Quaternion rotation)
        {
            Vector3 roomCenter = _beatSaberUtilities.roomCenter;
            Quaternion roomRotation = _beatSaberUtilities.roomRotation;

            position = Quaternion.Inverse(roomRotation) * position - roomCenter;
            rotation = Quaternion.Inverse(roomRotation) * rotation;
        }
    }
}
