using System;
using System.Collections;
using System.Collections.Generic;
using Il2CppAssets.Scripts.GameCore.HostComponent;
using Il2CppFormulaBase;
using MelonLoader;
using UnityEngine;

namespace MDEN.Managers
{
    internal static class AccuracyManager
    {
        private const float Precision = 0.0001f;
        private static readonly HashSet<float> SpecialValues = new() { 0.6f, 0.7f, 0.8f, 0.9f, 1f };
        private static readonly HashSet<short> PlayedNoteIds = new();
        private static readonly HashSet<short> MissedNoteIds = new();

        private static int _totalMusic;
        private static int _totalEnergy;
        private static int _totalHittable;
        private static int _totalBlock;
        private static int _currentPerfect;
        private static int _currentGreat;
        private static int _currentBlock;
        private static int _currentMusic;
        private static int _currentEnergy;
        private static int _currentRedPoint;
        private static int _missMonster;
        private static int _missLong;
        private static int _missLongPair;
        private static int _missGhost;
        private static int _missEnergy;
        private static int _missMusic;
        private static int _missRedPoint;
        private static int _missBlock;
        private static int _missMul;
        private static TaskStageTarget _taskStageTarget;
        private static StageBattleComponent _stageBattleComponent;

        internal static void Init()
        {
            ResetCounters();

            _taskStageTarget = TaskStageTarget.instance;
            _stageBattleComponent = StageBattleComponent.instance;

            if (_stageBattleComponent == null) return;

            try
            {
                var musicData = _stageBattleComponent.GetMusicData();
                if (musicData == null) return;

                foreach (var note in musicData)
                {
                    if (note == null || note.noteData == null) continue;

                    var type = note.noteData.type;
                    if (!note.isLongPressing && note.noteData.addCombo)
                    {
                        _totalHittable++;
                    }

                    switch (type)
                    {
                        case 2:
                            _totalBlock++;
                            break;
                        case 6:
                            _totalEnergy++;
                            break;
                        case 7:
                            _totalMusic++;
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                MDEN.Managers.ClientLogManager.Warning($"Accuracy manager init failed: {ex.Message}");
            }
        }

        internal static float GetCalculatedAccuracy()
        {
            UpdateCurrentStats();

            var total = AccuracyCalculationTotal;
            if (total <= 0f) return 100f;

            var accuracy = (AccuracyCalculationCounted + AccuracyCalculationRest) / total;
            var rounded = MathF.Round(accuracy / Precision) * Precision;
            return (accuracy < rounded && SpecialValues.Contains(rounded) ? rounded - Precision : rounded) * 100f;
        }

        internal static void HandleSetPlayResult(int idx, byte result, bool isMulStart, bool isMulEnd, bool isLeft)
        {
            if (_stageBattleComponent == null) return;

            var note = _stageBattleComponent.GetMusicDataByIdx(idx);
            if (note == null || note.noteData == null) return;

            var type = note.noteData.type;
            var oid = note.objId;

            switch (result)
            {
                case 4 when type == 2:
                    CountNote(oid, CountNoteAction.Block);
                    break;
                case 1 when type == 3:
                    CountNote(oid, CountNoteAction.MissLong, isLongStart: note.isLongPressStart);
                    break;
            }

            if (type == 8)
            {
                CountMul(oid, result, (float)note.configData.length);
            }
        }

        internal static void HandleMissCube(int idx, decimal currentTick)
        {
            if (_stageBattleComponent == null) return;

            try
            {
                var result = BattleEnemyManager.instance.GetPlayResult(idx);
                var note = _stageBattleComponent.GetMusicDataByIdx(idx);
                if (note == null || note.noteData == null) return;

                var type = note.noteData.type;
                var oid = note.objId;

                if (result == 0 || result == 1)
                {
                    switch (type)
                    {
                        case 4:
                            CountNote(oid, CountNoteAction.MissGhost);
                            break;
                        case 6:
                            CountNote(oid, CountNoteAction.MissEnergy);
                            break;
                        case 7:
                            CountNote(oid, CountNoteAction.MissMusic);
                            break;
                        case 2:
                            if (result != 0)
                            {
                                CountNote(oid, CountNoteAction.MissBlock);
                            }
                            break;
                        case 8:
                            break;
                        default:
                            short doubleOid = -1;
                            if (note.isDouble)
                            {
                                var doubleNote = _stageBattleComponent.GetMusicDataByIdx(note.doubleIdx);
                                if (doubleNote != null)
                                {
                                    doubleOid = doubleNote.objId;
                                }
                            }
                            CountNote(oid, CountNoteAction.MissMonster, doubleOid);
                            break;
                    }
                }

                if (type == 8)
                {
                    CountMul(oid, result, (float)note.configData.length);
                }
            }
            catch (Exception ex)
            {
                MDEN.Managers.ClientLogManager.Warning($"Accuracy miss handling failed: {ex.Message}");
            }
        }

        private static float AccuracyCalculationTotal => _totalMusic + _totalEnergy + _totalHittable + _totalBlock;
        private static float AccuracyCalculationCounted => _currentPerfect + _currentGreat / 2f + _currentBlock + _currentMusic + _currentEnergy + _currentRedPoint;
        private static float AccuracyCalculationRest => Math.Max(0f, GetAccuracyRest());

        private static int GetMissCountHittable() => _missMonster + _missLong + _missMul;
        private static int GetMissCountCollectible() => _missEnergy + _missMusic + _missRedPoint + _missGhost;
        private static int GetMissCount() => GetMissCountHittable() + GetMissCountCollectible() + _missBlock;

        private static float GetAccuracyRest()
        {
            return AccuracyCalculationTotal -
                   _currentPerfect -
                   _currentGreat -
                   _currentBlock -
                   _currentMusic -
                   _currentEnergy -
                   _currentRedPoint -
                   GetMissCount() -
                   _missLongPair;
        }

        private static void ResetCounters()
        {
            PlayedNoteIds.Clear();
            MissedNoteIds.Clear();

            _totalMusic = 0;
            _totalEnergy = 0;
            _totalHittable = 0;
            _totalBlock = 0;
            _currentPerfect = 0;
            _currentGreat = 0;
            _currentBlock = 0;
            _currentMusic = 0;
            _currentEnergy = 0;
            _currentRedPoint = 0;
            _missMonster = 0;
            _missLong = 0;
            _missLongPair = 0;
            _missGhost = 0;
            _missEnergy = 0;
            _missMusic = 0;
            _missRedPoint = 0;
            _missBlock = 0;
            _missMul = 0;
        }

        private static void UpdateCurrentStats()
        {
            if (_taskStageTarget == null) return;

            _currentPerfect = _taskStageTarget.m_PerfectResult;
            _currentGreat = _taskStageTarget.m_GreatResult;
            _currentMusic = _taskStageTarget.m_MusicCount;
            _currentEnergy = _taskStageTarget.m_EnergyCount;
            _currentRedPoint = _taskStageTarget.m_RedPoint;
        }

        private static void CountNote(short oid, CountNoteAction action, short doubleOid = -1, bool isLongStart = false, float time = 0f)
        {
            switch (action)
            {
                case CountNoteAction.Block:
                    if (PlayedNoteIds.Add(oid))
                    {
                        _currentBlock++;
                    }
                    break;
                case CountNoteAction.MissMonster:
                    CountMonsterMiss(oid, doubleOid);
                    break;
                case CountNoteAction.MissBlock:
                    if (MissedNoteIds.Add(oid))
                    {
                        _missBlock++;
                    }
                    if (!PlayedNoteIds.Add(oid))
                    {
                        _currentBlock--;
                    }
                    break;
                case CountNoteAction.MissLong:
                    if (MissedNoteIds.Add(oid))
                    {
                        _missLong++;
                        if (isLongStart)
                        {
                            _missLongPair++;
                        }
                    }
                    break;
                case CountNoteAction.MissGhost:
                    if (MissedNoteIds.Add(oid))
                    {
                        _missGhost++;
                    }
                    break;
                case CountNoteAction.MissEnergy:
                    if (MissedNoteIds.Add(oid))
                    {
                        _missEnergy++;
                    }
                    break;
                case CountNoteAction.MissMusic:
                    if (MissedNoteIds.Add(oid))
                    {
                        _missMusic++;
                    }
                    break;
                case CountNoteAction.Mul:
                    if (PlayedNoteIds.Add(oid) && MissedNoteIds.Remove(oid))
                    {
                        _missMul--;
                    }
                    break;
                case CountNoteAction.MissMul:
                    QueueMulMiss(oid, time);
                    break;
            }
        }

        private static void CountMonsterMiss(short oid, short doubleOid)
        {
            if (doubleOid == -1)
            {
                if (MissedNoteIds.Add(oid))
                {
                    _missMonster++;
                }
                return;
            }

            if (MissedNoteIds.Add(oid) && MissedNoteIds.Add(doubleOid))
            {
                _missMonster += 2;
            }
        }

        private static void CountMul(short oid, int result, float time)
        {
            switch (result)
            {
                case 0:
                case 1:
                    CountNote(oid, CountNoteAction.MissMul, time: time);
                    break;
                case 3:
                case 4:
                    CountNote(oid, CountNoteAction.Mul);
                    break;
            }
        }

        private static void QueueMulMiss(short oid, float time)
        {
            if (_stageBattleComponent == null) return;

            var currentTick = _stageBattleComponent.realTimeTick;
            MelonCoroutines.Start(DelayAction(() =>
            {
                if (_stageBattleComponent == null || _stageBattleComponent.realTimeTick <= currentTick)
                {
                    return;
                }

                if (!PlayedNoteIds.Contains(oid) && MissedNoteIds.Add(oid))
                {
                    _missMul++;
                }
            }, time));
        }

        private static IEnumerator DelayAction(Action action, float delay)
        {
            yield return new WaitForSeconds(delay);
            action?.Invoke();
        }

        private enum CountNoteAction
        {
            Block,
            Mul,
            MissMonster,
            MissBlock,
            MissLong,
            MissGhost,
            MissEnergy,
            MissMusic,
            MissMul
        }
    }
}
