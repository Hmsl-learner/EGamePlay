﻿using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using EGamePlay.Combat.Ability;
using ET;
using Log = EGamePlay.Log;
using UnityEngine.Timeline;
using UnityEngine.Playables;

namespace EGamePlay.Combat.Skill
{
    public class AnimationData
    {
        public bool HasStart;
        public bool HasEnded;
        public float StartTime;
        public float EndTime;
        public float Duration;
        public AnimationClip AnimationClip;
    }

    public class ColliderSpawnData
    {
        public bool HasStart;
        public ColliderSpawnEmitter ColliderSpawnEmitter;
    }

    /// <summary>
    /// 技能执行体
    /// </summary>
    public class SkillExecution : AbilityExecution
    {
        public SkillAbility SkillAbility { get { return AbilityEntity as SkillAbility; } }
        public CombatEntity InputCombatEntity { get; set; }
        public Vector3 InputPoint { get; set; }
        public float InputDirection { get; set; }
        public GameObject SkillExecutionAsset { get; set; }
        public long OriginTime { get; set; }
        public List<AnimationData> AnimationDatas { get; set; } = new List<AnimationData>();
        public List<ColliderSpawnData> ColliderSpawnDatas { get; set; } = new List<ColliderSpawnData>();


        public override void Awake(object initData)
        {
            base.Awake(initData);

            SkillExecutionAsset = Resources.Load<GameObject>($"Skill_{this.SkillAbility.SkillConfig.Id}_Execution");

            if (SkillExecutionAsset == null)
                return;
            var timelineAsset = SkillExecutionAsset.GetComponent<PlayableDirector>().playableAsset as TimelineAsset;
            if (timelineAsset == null)
                return;

            var markers = timelineAsset.markerTrack.GetMarkers();
            foreach (var item in markers)
            {
                if (item is ColliderSpawnEmitter colliderSpawnEmitter)
                {
                    ColliderSpawnDatas.Add(new ColliderSpawnData() { ColliderSpawnEmitter = colliderSpawnEmitter });
                }
            }

            var rootTracks = timelineAsset.GetRootTracks();
            foreach (var item in rootTracks)
            {
                if (item.hasClips)
                {
                    var clips = item.GetClips();
                    foreach (var clip in clips)
                    {
                        if (clip.animationClip != null)
                        {
                            var animationData = new AnimationData();
                            animationData.StartTime = (float)clip.clipIn;
                            animationData.Duration = (float)clip.duration;
                            animationData.EndTime = animationData.StartTime + animationData.Duration;
                            animationData.AnimationClip = clip.animationClip;
                            AnimationDatas.Add(animationData);
                        }
                    }
                }
            }

            OriginTime = ET.TimeHelper.Now();
        }

        public override void Update()
        {
            if (SkillAbility.Spelling == false)
            {
                return;
            }

            var nowSeconds = (double)(ET.TimeHelper.Now() - OriginTime) / 1000;
            foreach (var item in ColliderSpawnDatas)
            {
                if (item.HasStart == false)
                {
                    if (nowSeconds >= item.ColliderSpawnEmitter.time)
                    {
                        item.HasStart = true;
                        SpawnCollider(item.ColliderSpawnEmitter);
                    }
                }
            }

            var allAnimationEnded = true;
            foreach (var item in AnimationDatas)
            {
                if (item.HasStart == false)
                {
                    allAnimationEnded = false;
                    if (nowSeconds >= item.StartTime)
                    {
                        item.HasStart = true;
                        Hero.Instance.AnimationComponent.PlayFade(item.AnimationClip);
                    }
                }
                else
                {
                    if (item.HasEnded == false)
                    {
                        allAnimationEnded = false;
                        if (nowSeconds >= item.EndTime)
                        {
                            item.HasEnded = true;
                        }
                    }
                }
            }
            if (allAnimationEnded)
            {
                EndExecute();
            }
        }

        private void SpawnCollider(ColliderSpawnEmitter colliderSpawnEmitter)
        {
            if (colliderSpawnEmitter.ColliderType == ColliderType.TargetFly)
            {
                var taskData = new CastTargetFlyProjectileTaskData();
                taskData.FlyTime = 0.3f;
                taskData.TargetEntity = InputCombatEntity;
                var prefab = SkillExecutionAsset.transform.Find(colliderSpawnEmitter.ColliderName);
                taskData.ProjectilePrefab = prefab.gameObject;
                var task = Entity.CreateWithParent<CastTargetFlyProjectileTask>(OwnerEntity, taskData);
                task.OnEnterCallback = () => { AbilityEntity.ApplyAbilityEffectsTo(InputCombatEntity); };
                task.ExecuteTaskAsync().Coroutine();
            }
            if (colliderSpawnEmitter.ColliderType == ColliderType.ForwardFly)
            {
                var taskData = new CastDirectFlyProjectileTaskData();
                var prefab = SkillExecutionAsset.transform.Find(colliderSpawnEmitter.ColliderName);
                //var prefab = GetChildByName(SkillExecutionAsset.transform, colliderSpawnEmitter.ColliderName);
                taskData.ProjectilePrefab = prefab.gameObject;
                taskData.DirectAngle = InputDirection;
                var task = Entity.CreateWithParent<CastDirectFlyProjectileTask>(OwnerEntity, taskData);
                task.OnCollisionCallback = () => { Log.Debug($"CastDirectFlyProjectileTask OnCollisionCallback"); };
                task.AddComponent<UpdateComponent>();
                task.ExecuteTaskAsync().Coroutine();
            }
            if (colliderSpawnEmitter.ColliderType == ColliderType.FixedPosition)
            {
                var taskData = new CreateColliderTaskData();
                taskData.Position = InputPoint;
                var prefab = SkillExecutionAsset.transform.Find(colliderSpawnEmitter.ColliderName);
                taskData.ColliderPrefab = prefab.gameObject;
                taskData.LifeTime = (int)(colliderSpawnEmitter.ExistTime * 1000);
                taskData.OnTriggerEnterCallback = (other) => { AbilityEntity.ApplyAbilityEffectsTo(other.GetComponent<Monster>().CombatEntity); };
                var task = Entity.CreateWithParent<CreateColliderTask>(this, taskData);
                task.ExecuteTaskAsync().Coroutine();
            }
            if (colliderSpawnEmitter.ColliderType == ColliderType.FixedDirection)
            {
                var taskData = new CreateColliderTaskData();
                taskData.Position = OwnerEntity.Position;
                taskData.Direction = OwnerEntity.Direction;
                var prefab = SkillExecutionAsset.transform.Find(colliderSpawnEmitter.ColliderName);
                //var prefab = GetChildByName(SkillExecutionAsset.transform, colliderSpawnEmitter.ColliderName);
                taskData.ColliderPrefab = prefab.gameObject;
                taskData.LifeTime = (int)(colliderSpawnEmitter.ExistTime * 1000);
                taskData.OnTriggerEnterCallback = (other) => { AbilityEntity.ApplyAbilityEffectsTo(other.GetComponent<Monster>().CombatEntity); };
                var task = Entity.CreateWithParent<CreateColliderTask>(this, taskData);
                task.ExecuteTaskAsync().Coroutine();
            }
        }

        private Transform GetChildByName(Transform transform, string childName)
        {
            foreach (Transform item in transform)
            {
                if (item.name == childName)
                {
                    return item;
                }
            }
            return null;
        }

        public override void BeginExecute()
        {
            GetParent<CombatEntity>().CurrentSkillExecution = this;
            SkillAbility.Spelling = true;

            if (SkillExecutionAsset == null)
                return;
            var timelineAsset = SkillExecutionAsset.GetComponent<PlayableDirector>().playableAsset as TimelineAsset;
            if (timelineAsset == null)
                return;
            var skillExecutionObj = GameObject.Instantiate(SkillExecutionAsset, Hero.Instance.transform.position, Hero.Instance.transform.GetChild(0).rotation);
            GameObject.Destroy(skillExecutionObj, (float)timelineAsset.duration);
            base.BeginExecute();
        }

        public override void EndExecute()
        {
            GetParent<CombatEntity>().CurrentSkillExecution = null;
            SkillAbility.Spelling = false;
            Hero.Instance.AnimationComponent.PlayFade(Hero.Instance.AnimationComponent.IdleAnimation);
            base.EndExecute();
        }
    }
}