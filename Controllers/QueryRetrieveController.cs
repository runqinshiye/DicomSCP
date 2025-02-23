using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using FellowOakDicom;
using DicomSCP.Services;
using DicomSCP.Configuration;
using DicomSCP.Models;
using FellowOakDicom.Network;

namespace DicomSCP.Controllers;

// 在文件开头添加传输语法枚举
public enum DicomTransferSyntaxType
{
    /// <summary>
    /// 隐式 VR Little Endian - 默认传输语法
    /// UID: 1.2.840.10008.1.2
    /// Code: IVLE
    /// </summary>
    ImplicitVRLittleEndian,
    
    /// <summary>
    /// 显式 VR Little Endian - 常用于网络传输
    /// UID: 1.2.840.10008.1.2.1
    /// Code: EVLE
    /// </summary>
    ExplicitVRLittleEndian,
    
    /// <summary>
    /// 显式 VR Big Endian - 已弃用，但某些老设备可能需要
    /// UID: 1.2.840.10008.1.2.2
    /// Code: EVBE
    /// </summary>
    ExplicitVRBigEndian,
    
    /// <summary>
    /// JPEG Baseline (Process 1) - 有损压缩
    /// UID: 1.2.840.10008.1.2.4.50
    /// Code: JPEG_BASELINE
    /// </summary>
    JPEGBaseline,
    
    /// <summary>
    /// JPEG Lossless (Process 14) - 无损压缩
    /// UID: 1.2.840.10008.1.2.4.57
    /// Code: JPEG_LOSSLESS
    /// </summary>
    JPEGLossless,
    
    /// <summary>
    /// JPEG 2000 有损压缩
    /// UID: 1.2.840.10008.1.2.4.91
    /// Code: JPEG2000_LOSSY
    /// </summary>
    JPEG2000Lossy,
    
    /// <summary>
    /// JPEG 2000 无损压缩
    /// UID: 1.2.840.10008.1.2.4.90
    /// Code: JPEG2000_LOSSLESS
    /// </summary>
    JPEG2000Lossless,
    
    /// <summary>
    /// RLE 无损压缩
    /// UID: 1.2.840.10008.1.2.5
    /// Code: RLE
    /// </summary>
    RLELossless,
    
    /// <summary>
    /// JPEG-LS 无损压缩
    /// UID: 1.2.840.10008.1.2.4.80
    /// Code: JPEGLS_LOSSLESS
    /// </summary>
    JPEGLSLossless,
    
    /// <summary>
    /// JPEG-LS 近无损压缩
    /// UID: 1.2.840.10008.1.2.4.81
    /// Code: JPEGLS_NEAR_LOSSLESS
    /// </summary>
    JPEGLSNearLossless
}

// 添加传输语法扩展方法
public static class DicomTransferSyntaxExtensions
{
    public static string GetUID(this DicomTransferSyntaxType transferSyntax)
    {
        return transferSyntax switch
        {
            DicomTransferSyntaxType.ImplicitVRLittleEndian => "1.2.840.10008.1.2",
            DicomTransferSyntaxType.ExplicitVRLittleEndian => "1.2.840.10008.1.2.1",
            DicomTransferSyntaxType.ExplicitVRBigEndian => "1.2.840.10008.1.2.2",
            DicomTransferSyntaxType.JPEGBaseline => "1.2.840.10008.1.2.4.50",
            DicomTransferSyntaxType.JPEGLossless => "1.2.840.10008.1.2.4.57",
            DicomTransferSyntaxType.JPEG2000Lossy => "1.2.840.10008.1.2.4.91",
            DicomTransferSyntaxType.JPEG2000Lossless => "1.2.840.10008.1.2.4.90",
            DicomTransferSyntaxType.RLELossless => "1.2.840.10008.1.2.5",
            DicomTransferSyntaxType.JPEGLSLossless => "1.2.840.10008.1.2.4.80",
            DicomTransferSyntaxType.JPEGLSNearLossless => "1.2.840.10008.1.2.4.81",
            _ => throw new ArgumentException($"不支持的传输语法类型: {transferSyntax}")
        };
    }

    public static string GetDescription(this DicomTransferSyntaxType transferSyntax)
    {
        return transferSyntax switch
        {
            DicomTransferSyntaxType.ImplicitVRLittleEndian => "隐式 VR Little Endian (默认)",
            DicomTransferSyntaxType.ExplicitVRLittleEndian => "显式 VR Little Endian",
            DicomTransferSyntaxType.ExplicitVRBigEndian => "显式 VR Big Endian",
            DicomTransferSyntaxType.JPEGBaseline => "JPEG Baseline (有损)",
            DicomTransferSyntaxType.JPEGLossless => "JPEG 无损",
            DicomTransferSyntaxType.JPEG2000Lossy => "JPEG 2000 有损",
            DicomTransferSyntaxType.JPEG2000Lossless => "JPEG 2000 无损",
            DicomTransferSyntaxType.RLELossless => "RLE 无损",
            DicomTransferSyntaxType.JPEGLSLossless => "JPEG-LS 无损",
            DicomTransferSyntaxType.JPEGLSNearLossless => "JPEG-LS 近无损",
            _ => throw new ArgumentException($"不支持的传输语法类型: {transferSyntax}")
        };
    }
}

// 添加传输语法解析和验证类
public static class DicomTransferSyntaxParser
{
    private static readonly Dictionary<string, string> _uidMap = new()
    {
        { "1.2.840.10008.1.2", "ImplicitVRLittleEndian" },
        { "1.2.840.10008.1.2.1", "ExplicitVRLittleEndian" },
        { "1.2.840.10008.1.2.2", "ExplicitVRBigEndian" },
        { "1.2.840.10008.1.2.4.50", "JPEGBaseline" },
        { "1.2.840.10008.1.2.4.57", "JPEGLossless" },
        { "1.2.840.10008.1.2.4.91", "JPEG2000Lossy" },
        { "1.2.840.10008.1.2.4.90", "JPEG2000Lossless" },
        { "1.2.840.10008.1.2.5", "RLELossless" },
        { "1.2.840.10008.1.2.4.80", "JPEGLSLossless" },
        { "1.2.840.10008.1.2.4.81", "JPEGLSNearLossless" }
    };

    private static readonly Dictionary<string, string> _codeMap = new()
    {
        { "IVLE", "ImplicitVRLittleEndian" },
        { "EVLE", "ExplicitVRLittleEndian" },
        { "EVBE", "ExplicitVRBigEndian" },
        { "JPEG_BASELINE", "JPEGBaseline" },
        { "JPEG_LOSSLESS", "JPEGLossless" },
        { "JPEG2000_LOSSY", "JPEG2000Lossy" },
        { "JPEG2000_LOSSLESS", "JPEG2000Lossless" },
        { "RLE", "RLELossless" },
        { "JPEGLS_LOSSLESS", "JPEGLSLossless" },
        { "JPEGLS_NEAR_LOSSLESS", "JPEGLSNearLossless" }
    };

    public static DicomTransferSyntaxType? Parse(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        // 1. 尝试直接解析枚举名称
        if (Enum.TryParse<DicomTransferSyntaxType>(value, true, out var result))
            return result;

        // 2. 尝试从 UID 映射
        if (_uidMap.TryGetValue(value, out var uidMapped))
            if (Enum.TryParse<DicomTransferSyntaxType>(uidMapped, true, out result))
                return result;

        // 3. 尝试从代码映射
        if (_codeMap.TryGetValue(value.ToUpper(), out var codeMapped))
            if (Enum.TryParse<DicomTransferSyntaxType>(codeMapped, true, out result))
                return result;

        throw new ArgumentException($"不支持的传输语法: {value}");
    }
}

[ApiController]
[Route("api/[controller]")]
public class QueryRetrieveController : ControllerBase
{
    private readonly IQueryRetrieveSCU _queryRetrieveScu;
    private readonly QueryRetrieveConfig _config;
    private readonly DicomSettings _settings;
    private const string LogPrefix = "[Api]";

    public QueryRetrieveController(
        IQueryRetrieveSCU queryRetrieveScu,
        IOptions<QueryRetrieveConfig> config,
        IOptions<DicomSettings> settings)
    {
        _queryRetrieveScu = queryRetrieveScu;
        _config = config.Value;
        _settings = settings.Value;
    }

    [HttpGet("nodes")]
    public ActionResult<IEnumerable<RemoteNode>> GetNodes()
    {
        // 只返回支持查询检索的节点
        var qrNodes = _config.RemoteNodes.Where(n => n.SupportsQueryRetrieve());
        return Ok(qrNodes);
    }

    // 统一的查询接口
    [HttpPost("{nodeId}/query")]
    public async Task<ActionResult<IEnumerable<object>>> Query(
        string nodeId, 
        [FromQuery] string level,
        [FromBody] QueryRequest queryParams)
    {
        var node = _config.RemoteNodes.FirstOrDefault(n => n.Name == nodeId);
        if (node == null)
        {
            return NotFound($"未找到节点: {nodeId}");
        }

        // 验证节点是否支持查询检索
        if (!node.SupportsQueryRetrieve())
        {
            return BadRequest($"节点 {nodeId} 不支持查询检索操作");
        }

        // 解析查询级别
        if (!Enum.TryParse<DicomQueryRetrieveLevel>(level, true, out var queryLevel))
        {
            return BadRequest($"无效的查询级别: {level}。有效值为: Patient, Study, Series, Image");
        }

        // Patient级别查询参数验证
        if (queryLevel == DicomQueryRetrieveLevel.Patient && !string.IsNullOrEmpty(queryParams.StudyInstanceUid))
        {
            DicomLogger.Warning(LogPrefix, "Patient级别查询不应包含StudyInstanceUID");
        }

        // Image级别查询的特殊验证
        if (queryLevel == DicomQueryRetrieveLevel.Image && !queryParams.ValidateImageLevelQuery())
        {
            return BadRequest(new QueryResponse<object>
            {
                Success = false,
                Message = "Image级别查询必须提供StudyInstanceUID和SeriesInstanceUID"
            });
        }

        try
        {
            var queryDict = new Dictionary<string, string>();
            
            if (!string.IsNullOrEmpty(queryParams.PatientId))
                queryDict["patientId"] = queryParams.PatientId;
            if (!string.IsNullOrEmpty(queryParams.PatientName))
                queryDict["patientName"] = queryParams.PatientName;
            if (!string.IsNullOrEmpty(queryParams.AccessionNumber))
                queryDict["accessionNumber"] = queryParams.AccessionNumber;
            if (!string.IsNullOrEmpty(queryParams.StudyDate))
                queryDict["studyDate"] = queryParams.StudyDate;
            if (!string.IsNullOrEmpty(queryParams.Modality))
                queryDict["modality"] = queryParams.Modality;
            if (!string.IsNullOrEmpty(queryParams.StudyInstanceUid))
                queryDict["studyInstanceUid"] = queryParams.StudyInstanceUid;
            if (!string.IsNullOrEmpty(queryParams.SeriesInstanceUid))
                queryDict["seriesInstanceUid"] = queryParams.SeriesInstanceUid;

            // Image级别特有参数 - 不再添加 sopInstanceUid 作为查询条件
            if (!string.IsNullOrEmpty(queryParams.InstanceNumber))
            {
                queryDict["instanceNumber"] = queryParams.InstanceNumber;
                DicomLogger.Debug(LogPrefix, "添加InstanceNumber参数: {0}", queryParams.InstanceNumber);
            }

            DicomLogger.Debug(LogPrefix, "查询参数字典: {0}", 
                string.Join(", ", queryDict.Select(kv => $"{kv.Key}={kv.Value}")));

            var dataset = BuildQueryDataset(queryLevel, queryDict);
            var results = await _queryRetrieveScu.QueryAsync(node, queryLevel, dataset);

            // 记录原始结果
            DicomLogger.Debug(LogPrefix, "收到查询结果 - Level: {Level}, Count: {Count}", 
                queryLevel, results?.Count() ?? 0);

            // 确保结果不为空
            if (results == null || !results.Any())
            {
                DicomLogger.Warning(LogPrefix, "查询未返回数据 - Level: {Level}, Node: {Node}", 
                    queryLevel, nodeId);
                return Ok(new QueryResponse<object>
                {
                    Success = true,
                    Data = Array.Empty<object>(),
                    Total = 0,
                    Message = "未找到匹配的数据"
                });
            }

            // Image级别的特殊处理
            if (queryLevel == DicomQueryRetrieveLevel.Image)
            {
                var imageResults = results.Select(DicomImageResult.FromDataset).ToList();
                DicomLogger.Information(LogPrefix, "Image级别查询返回 {0} 条结果", imageResults.Count);
                return Ok(new QueryResponse<DicomImageResult>
                {
                    Success = true,
                    Data = imageResults,
                    Total = imageResults.Count
                });
            }

            // 根据不同级别返回不同的响应类型
            switch (queryLevel)
            {
                case DicomQueryRetrieveLevel.Patient:
                    return Ok(new QueryResponse<DicomPatientResult>
                    {
                        Success = true,
                        Data = results.Select(DicomPatientResult.FromDataset),
                        Total = results.Count()
                    });
                case DicomQueryRetrieveLevel.Study:
                    return Ok(new QueryResponse<DicomStudyResult>
                    {
                        Success = true,
                        Data = results.Select(DicomStudyResult.FromDataset),
                        Total = results.Count()
                    });
                case DicomQueryRetrieveLevel.Series:
                    return Ok(new QueryResponse<DicomSeriesResult>
                    {
                        Success = true,
                        Data = results.Select(DicomSeriesResult.FromDataset),
                        Total = results.Count()
                    });
                case DicomQueryRetrieveLevel.Image:
                    var imageResults = results.Select(DicomImageResult.FromDataset).ToList();
                    DicomLogger.Information(LogPrefix, "Image级别查询返回 {0} 条结果", imageResults.Count);
                    return Ok(new QueryResponse<DicomImageResult>
                    {
                        Success = true,
                        Data = imageResults,
                        Total = imageResults.Count
                    });
                default:
                    return BadRequest(new QueryResponse<object>
                    {
                        Success = false,
                        Message = $"不支持的查询级别: {level}"
                    });
            }
        }
        catch (Exception ex)
        {
            DicomLogger.Error(LogPrefix, ex, "执行{Level}查询失败", level);
            return StatusCode(500, new QueryResponse<object>
            {
                Success = false,
                Message = ex.Message
            });
        }
    }

    /// <summary>
    /// 发送DICOM获取请求
    /// </summary>
    [HttpPost("{nodeId}/move")]
    [ProducesResponseType(typeof(MoveResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MoveResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MoveResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(MoveResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> Move(
        string nodeId,
        [FromQuery] string level,
        [FromBody] MoveRequest moveRequest)
    {
        try
        {
            var node = _config.RemoteNodes.FirstOrDefault(n => n.Name == nodeId);
            if (node == null)
            {
                return NotFound($"未找到节点: {nodeId}");
            }

            // 验证节点是否支持查询检索
            if (!node.SupportsQueryRetrieve())
            {
                return BadRequest($"节点 {nodeId} 不支持查询检索操作");
            }

            // 解析级别
            if (!Enum.TryParse<DicomQueryRetrieveLevel>(level, true, out var queryLevel))
            {
                return BadRequest(new MoveResponse 
                { 
                    Success = false,
                    Message = $"无效的获取级别: {level}。有效值为: Patient, Study, Series, Image"
                });
            }

            // 验证请求参数
            var (isValid, errorMessage) = ValidateMoveRequest(queryLevel, moveRequest);
            if (!isValid)
            {
                return BadRequest(new MoveResponse 
                { 
                    Success = false,
                    Message = errorMessage
                });
            }

            try
            {
                // 构建数据集
                var dataset = new DicomDataset();
                dataset.Add(DicomTag.QueryRetrieveLevel, queryLevel.ToString().ToUpper());

                // 根据不同级别添加必要的字段
                switch (queryLevel)
                {
                    case DicomQueryRetrieveLevel.Patient:
                        dataset.Add(DicomTag.PatientID, moveRequest.PatientId);
                        break;

                    case DicomQueryRetrieveLevel.Study:
                        dataset.Add(DicomTag.StudyInstanceUID, moveRequest.StudyInstanceUid);
                        break;

                    case DicomQueryRetrieveLevel.Series:
                        dataset.Add(DicomTag.StudyInstanceUID, moveRequest.StudyInstanceUid);
                        dataset.Add(DicomTag.SeriesInstanceUID, moveRequest.SeriesInstanceUid);
                        break;

                    case DicomQueryRetrieveLevel.Image:
                        dataset.Add(DicomTag.StudyInstanceUID, moveRequest.StudyInstanceUid);
                        dataset.Add(DicomTag.SeriesInstanceUID, moveRequest.SeriesInstanceUid);
                        dataset.Add(DicomTag.SOPInstanceUID, moveRequest.SopInstanceUid);
                        break;
                }

                DicomLogger.Debug(LogPrefix, "Move请求数据集: {0}", dataset.ToString());

                // 解析传输语法
                string? transferSyntax = null;
                if (!string.IsNullOrEmpty(moveRequest.TransferSyntax))
                {
                    try
                    {
                        var syntaxType = DicomTransferSyntaxParser.Parse(moveRequest.TransferSyntax);
                        if (syntaxType.HasValue)
                        {
                            transferSyntax = syntaxType.Value.GetUID();
                            DicomLogger.Debug(LogPrefix, 
                                "使用指定的传输语法: {0} ({1}) [输入: {2}]", 
                                syntaxType.Value.GetDescription(),
                                transferSyntax,
                                moveRequest.TransferSyntax);
                        }
                    }
                    catch (ArgumentException ex)
                    {
                        return BadRequest(new MoveResponse 
                        { 
                            Success = false,
                            Message = ex.Message
                        });
                    }
                }

                // 直接使用本地 AE Title，并传入传输语法参数
                var success = await _queryRetrieveScu.MoveAsync(
                    node, 
                    queryLevel, 
                    dataset, 
                    _settings.AeTitle,
                    transferSyntax);
                
                if (!success)
                {
                    // 根据不同情况返回不同的错误信息
                    return StatusCode(500, new MoveResponse 
                    { 
                        Success = false,
                        Message = queryLevel == DicomQueryRetrieveLevel.Patient ? 
                            "Patient级别获取未返回任何影像，该级别可能不被支持，请尝试使用Study级别获取" : 
                            "获取请求被拒绝"
                    });
                }

                return Ok(new MoveResponse
                {
                    Success = true,
                    Message = queryLevel == DicomQueryRetrieveLevel.Patient ? 
                        "Patient级别获取请求已发送，如果支持此级别操作，稍后可在影像管理中查看" : 
                        "获取请求已发送，请稍后在影像管理中查看",
                    JobId = Guid.NewGuid().ToString()
                });
            }
            catch (Exception ex)
            {
                DicomLogger.Error(LogPrefix, ex, "发送{Level}获取请求失败", level);
                return StatusCode(500, new MoveResponse 
                { 
                    Success = false,
                    Message = "发送获取请求失败" 
                });
            }
        }
        catch (Exception ex)
        {
            DicomLogger.Error(LogPrefix, ex, "执行{Level}获取请求失败", level);
            return StatusCode(500, new MoveResponse 
            { 
                Success = false,
                Message = "执行获取请求失败" 
            });
        }
    }

    // 添加验证方法
    private (bool IsValid, string ErrorMessage) ValidateMoveRequest(DicomQueryRetrieveLevel level, MoveRequest request)
    {
        switch (level)
        {
            case DicomQueryRetrieveLevel.Patient:
                if (string.IsNullOrEmpty(request.PatientId))
                {
                    return (false, "Patient级别获取必须提供PatientId");
                }
                break;

            case DicomQueryRetrieveLevel.Study:
                if (string.IsNullOrEmpty(request.StudyInstanceUid))
                {
                    return (false, "Study级别获取必须提供StudyInstanceUID");
                }
                break;

            case DicomQueryRetrieveLevel.Series:
                if (string.IsNullOrEmpty(request.StudyInstanceUid) || 
                    string.IsNullOrEmpty(request.SeriesInstanceUid))
                {
                    return (false, "Series级别获取必须提供StudyInstanceUID和SeriesInstanceUID");
                }
                break;

            case DicomQueryRetrieveLevel.Image:
                if (string.IsNullOrEmpty(request.StudyInstanceUid) || 
                    string.IsNullOrEmpty(request.SeriesInstanceUid) || 
                    string.IsNullOrEmpty(request.SopInstanceUid))
                {
                    return (false, "Image级别获取必须提供StudyInstanceUID、SeriesInstanceUID和SopInstanceUID");
                }
                break;
        }

        return (true, string.Empty);
    }

    // 辅助方法：构建查询数据集
    private DicomDataset BuildQueryDataset(DicomQueryRetrieveLevel level, Dictionary<string, string> queryParams)
    {
        var dataset = new DicomDataset();
        dataset.Add(DicomTag.QueryRetrieveLevel, level.ToString().ToUpper());

        switch (level)
        {
            case DicomQueryRetrieveLevel.Patient:
                AddPatientQueryFields(dataset, queryParams);
                break;
            case DicomQueryRetrieveLevel.Study:
                AddStudyQueryFields(dataset, queryParams);
                break;
            case DicomQueryRetrieveLevel.Series:
                AddSeriesQueryFields(dataset, queryParams);
                break;
            case DicomQueryRetrieveLevel.Image:
                AddImageQueryFields(dataset, queryParams);
                break;
        }

        return dataset;
    }

    // 辅助方法：格式化查询结果
    private object FormatQueryResults(DicomQueryRetrieveLevel level, IEnumerable<DicomDataset> results)
    {
        switch (level)
        {
            case DicomQueryRetrieveLevel.Study:
                return results.Select(DicomStudyResult.FromDataset);
            case DicomQueryRetrieveLevel.Series:
                return results.Select(DicomSeriesResult.FromDataset);
            case DicomQueryRetrieveLevel.Image:
                return results.Select(DicomImageResult.FromDataset);
            default:
                return results;
        }
    }

    // 辅助方法：构建移动数据集
    private DicomDataset BuildMoveDataset(DicomQueryRetrieveLevel level, Dictionary<string, string> moveRequest)
    {
        var dataset = new DicomDataset();
        dataset.Add(DicomTag.QueryRetrieveLevel, level.ToString().ToUpper());

        switch (level)
        {
            case DicomQueryRetrieveLevel.Study:
                if (moveRequest.TryGetValue("studyInstanceUid", out string? studyUid1) && !string.IsNullOrEmpty(studyUid1))
                {
                    dataset.Add(DicomTag.StudyInstanceUID, studyUid1);
                }
                break;

            case DicomQueryRetrieveLevel.Series:
                if (moveRequest.TryGetValue("studyInstanceUid", out string? studyUid2) && !string.IsNullOrEmpty(studyUid2))
                {
                    dataset.Add(DicomTag.StudyInstanceUID, studyUid2);
                }
                if (moveRequest.TryGetValue("seriesInstanceUid", out string? seriesUid1) && !string.IsNullOrEmpty(seriesUid1))
                {
                    dataset.Add(DicomTag.SeriesInstanceUID, seriesUid1);
                }
                break;

            case DicomQueryRetrieveLevel.Image:
                if (moveRequest.TryGetValue("studyInstanceUid", out string? studyUid3) && !string.IsNullOrEmpty(studyUid3))
                {
                    dataset.Add(DicomTag.StudyInstanceUID, studyUid3);
                }
                if (moveRequest.TryGetValue("seriesInstanceUid", out string? seriesUid2) && !string.IsNullOrEmpty(seriesUid2))
                {
                    dataset.Add(DicomTag.SeriesInstanceUID, seriesUid2);
                }
                if (moveRequest.TryGetValue("sopInstanceUid", out string? sopUid) && !string.IsNullOrEmpty(sopUid))
                {
                    dataset.Add(DicomTag.SOPInstanceUID, sopUid);
                }
                break;
        }

        return dataset;
    }

    // 添加病人查询字段
    private void AddPatientQueryFields(DicomDataset dataset, Dictionary<string, string> queryParams)
    {
        // 必需返回的字段
        dataset.Add(DicomTag.PatientID, "");
        dataset.Add(DicomTag.PatientName, "");
        dataset.Add(DicomTag.PatientBirthDate, "");
        dataset.Add(DicomTag.PatientSex, "");
        dataset.Add(DicomTag.NumberOfPatientRelatedStudies, "");

        // 只处理 Patient 级别的查询条件
        if (queryParams.TryGetValue("patientId", out var patientId) && !string.IsNullOrWhiteSpace(patientId))
        {
            dataset.AddOrUpdate(DicomTag.PatientID, patientId);
            DicomLogger.Debug(LogPrefix, "Patient查询 - 添加PatientID: {0}", patientId);
        }

        if (queryParams.TryGetValue("patientName", out var patientName) && !string.IsNullOrWhiteSpace(patientName))
        {
            dataset.AddOrUpdate(DicomTag.PatientName, $"*{patientName}*");
            DicomLogger.Debug(LogPrefix, "Patient查询 - 添加PatientName: {0}", patientName);
        }

        if (queryParams.TryGetValue("patientBirthDate", out var birthDate) && !string.IsNullOrWhiteSpace(birthDate))
        {
            dataset.AddOrUpdate(DicomTag.PatientBirthDate, birthDate);
            DicomLogger.Debug(LogPrefix, "Patient查询 - 添加PatientBirthDate: {0}", birthDate);
        }

        if (queryParams.TryGetValue("patientSex", out var sex) && !string.IsNullOrWhiteSpace(sex))
        {
            dataset.AddOrUpdate(DicomTag.PatientSex, sex);
            DicomLogger.Debug(LogPrefix, "Patient查询 - 添加PatientSex: {0}", sex);
        }

        // 忽略其他级别的查询条件（如 studyInstanceUid）
        if (queryParams.ContainsKey("studyInstanceUid"))
        {
            DicomLogger.Warning(LogPrefix, "Patient查询不应使用StudyInstanceUID作为查询条件");
        }

        DicomLogger.Debug(LogPrefix, "Patient级别查询数据集: {0}", dataset.ToString());
    }

    // 添加检级别查询字段
    private void AddStudyQueryFields(DicomDataset dataset, Dictionary<string, string> queryParams)
    {
        // 必需返回的字段
        dataset.Add(DicomTag.StudyInstanceUID, "");
        dataset.Add(DicomTag.StudyDate, "");
        dataset.Add(DicomTag.StudyTime, "");
        dataset.Add(DicomTag.PatientName, "");
        dataset.Add(DicomTag.PatientID, "");
        dataset.Add(DicomTag.StudyDescription, "");
        dataset.Add(DicomTag.ModalitiesInStudy, "");
        dataset.Add(DicomTag.NumberOfStudyRelatedSeries, "");
        dataset.Add(DicomTag.NumberOfStudyRelatedInstances, "");
        dataset.Add(DicomTag.AccessionNumber, "");

        // 处理查询条件
        if (queryParams.TryGetValue("patientId", out var patientId) && !string.IsNullOrWhiteSpace(patientId))
        {
            dataset.AddOrUpdate(DicomTag.PatientID, $"*{patientId}*");
        }

        if (queryParams.TryGetValue("patientName", out var patientName) && !string.IsNullOrWhiteSpace(patientName))
        {
            dataset.AddOrUpdate(DicomTag.PatientName, $"*{patientName}*");
        }

        if (queryParams.TryGetValue("accessionNumber", out var accessionNumber) && !string.IsNullOrWhiteSpace(accessionNumber))
        {
            dataset.AddOrUpdate(DicomTag.AccessionNumber, $"*{accessionNumber}*");
        }

        if (queryParams.TryGetValue("modality", out var modality) && !string.IsNullOrWhiteSpace(modality))
        {
            dataset.AddOrUpdate(DicomTag.ModalitiesInStudy, modality);
        }

        if (queryParams.TryGetValue("studyDate", out var studyDate) && !string.IsNullOrWhiteSpace(studyDate))
        {
            // 转换为 DICOM 日期格式 YYYYMMDD
            var dicomDate = studyDate.Replace("-", "");
            dataset.AddOrUpdate(DicomTag.StudyDate, dicomDate);
        }
    }

    // 添加序列级别查询字段
    private void AddSeriesQueryFields(DicomDataset dataset, Dictionary<string, string> queryParams)
    {
        // 必需返回的字段
        dataset.Add(DicomTag.SeriesInstanceUID, "");
        dataset.Add(DicomTag.StudyInstanceUID, "");  // 添加StudyInstanceUID
        dataset.Add(DicomTag.SeriesNumber, "");
        dataset.Add(DicomTag.SeriesDescription, "");
        dataset.Add(DicomTag.Modality, "");
        dataset.Add(DicomTag.NumberOfSeriesRelatedInstances, "");

        // 必需的上级字段
        if (queryParams.TryGetValue("studyInstanceUid", out var studyUid))
        {
            dataset.AddOrUpdate(DicomTag.StudyInstanceUID, studyUid);
        }

        // 处理查询条件
        if (queryParams.TryGetValue("seriesInstanceUid", out var seriesUid))
        {
            dataset.AddOrUpdate(DicomTag.SeriesInstanceUID, seriesUid);
        }

        if (queryParams.TryGetValue("seriesNumber", out var seriesNumber))
        {
            dataset.AddOrUpdate(DicomTag.SeriesNumber, seriesNumber);
        }

        if (queryParams.TryGetValue("seriesDescription", out var seriesDescription))
        {
            dataset.AddOrUpdate(DicomTag.SeriesDescription, seriesDescription);
        }

        if (queryParams.TryGetValue("seriesModality", out var modality))
        {
            dataset.AddOrUpdate(DicomTag.Modality, modality);
        }
    }

    // 添加影像级别查询字段
    private void AddImageQueryFields(DicomDataset dataset, Dictionary<string, string> queryParams)
    {
        // 必需返回的字段
        dataset.Add(DicomTag.SOPInstanceUID, "");
        dataset.Add(DicomTag.StudyInstanceUID, "");
        dataset.Add(DicomTag.SeriesInstanceUID, "");
        dataset.Add(DicomTag.InstanceNumber, "");
        dataset.Add(DicomTag.ImageType, "");
        dataset.Add(DicomTag.Rows, "");
        dataset.Add(DicomTag.Columns, "");
        dataset.Add(DicomTag.BitsAllocated, "");
        dataset.Add(DicomTag.NumberOfFrames, "");
        dataset.Add(DicomTag.SOPClassUID, "");

        // 必需的上级字段 - 这些是用来限定查询范围的
        if (queryParams.TryGetValue("studyInstanceUid", out var studyUid))
        {
            dataset.AddOrUpdate(DicomTag.StudyInstanceUID, studyUid);
            DicomLogger.Debug(LogPrefix, "添加StudyInstanceUID: {0}", studyUid);
        }
        if (queryParams.TryGetValue("seriesInstanceUid", out var seriesUid))
        {
            dataset.AddOrUpdate(DicomTag.SeriesInstanceUID, seriesUid);
            DicomLogger.Debug(LogPrefix, "添加SeriesInstanceUID: {0}", seriesUid);
        }

        // 注意：通常不应该在查询时使用 SOPInstanceUID，
        // 因为我们想要获取序列下的所有图像
        // 如果需要特定图像，应该在结果中过滤

        if (queryParams.TryGetValue("instanceNumber", out var instanceNumber))
        {
            dataset.AddOrUpdate(DicomTag.InstanceNumber, instanceNumber);
            DicomLogger.Debug(LogPrefix, "添加InstanceNumber: {0}", instanceNumber);
        }

        // 记录完整的查询数据集
        DicomLogger.Debug(LogPrefix, "Image级别查询数据集: {0}", dataset.ToString());
    }

    [HttpPost("{nodeId}/verify")]
    public async Task<IActionResult> VerifyConnection(string nodeId)
    {
        try
        {
            var node = _config.RemoteNodes.FirstOrDefault(n => n.Name == nodeId);
            if (node == null)
            {
                return NotFound($"未找到节点: {nodeId}");
            }

            // 验证节点是否支持查询检索
            if (!node.SupportsQueryRetrieve())
            {
                return BadRequest($"节点 {nodeId} 不支持查询检索操作");
            }

            var success = await _queryRetrieveScu.VerifyConnectionAsync(node);
            
            if (!success)
            {
                return StatusCode(500, new { 
                    success = false,
                    message = "连接测试失败",
                    details = new {
                        localAe = _config.LocalAeTitle,
                        remoteAe = node.AeTitle,
                        host = node.HostName,
                        port = node.Port
                    }
                });
            }

            return Ok(new { 
                success = true,
                message = "连接测试成功",
                details = new {
                    localAe = _config.LocalAeTitle,
                    remoteAe = node.AeTitle,
                    host = node.HostName,
                    port = node.Port
                }
            });
        }
        catch (Exception ex)
        {
            DicomLogger.Error(LogPrefix, ex, "执行连接测试失败");
            return StatusCode(500, new { success = false, message = "执行连接测试失败", error = ex.Message });
        }
    }

    // ... 其他辅助方法
}

// 添加用于转换查询结果的类
public class DicomStudyResult
{
    private const string LogPrefix = "[Api]";

    public string PatientId { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string AccessionNumber { get; set; } = string.Empty;
    public string Modality { get; set; } = string.Empty;
    public DateTime? StudyDate { get; set; }
    public string StudyDescription { get; set; } = string.Empty;
    public string StudyInstanceUid { get; set; } = string.Empty;
    public int NumberOfSeries { get; set; }
    public int NumberOfInstances { get; set; }

    public static DicomStudyResult FromDataset(DicomDataset dataset)
    {
        DateTime? studyDate = null;
        try
        {
            var dateStr = dataset.GetSingleValueOrDefault(DicomTag.StudyDate, string.Empty);
            if (!string.IsNullOrEmpty(dateStr) && dateStr.Length == 8)
            {
                var year = int.Parse(dateStr.Substring(0, 4));
                var month = int.Parse(dateStr.Substring(4, 2));
                var day = int.Parse(dateStr.Substring(6, 2));
                studyDate = new DateTime(year, month, day);
            }
        }
        catch (Exception ex)
        {
            DicomLogger.Warning(LogPrefix, ex, "解析研究日期失败");
        }

        return new DicomStudyResult
        {
            PatientId = dataset.GetSingleValueOrDefault(DicomTag.PatientID, string.Empty),
            PatientName = dataset.GetSingleValueOrDefault(DicomTag.PatientName, string.Empty),
            AccessionNumber = dataset.GetSingleValueOrDefault(DicomTag.AccessionNumber, string.Empty),
            Modality = dataset.GetSingleValueOrDefault(DicomTag.ModalitiesInStudy, string.Empty),
            StudyDate = studyDate,
            StudyDescription = dataset.GetSingleValueOrDefault(DicomTag.StudyDescription, string.Empty),
            StudyInstanceUid = dataset.GetSingleValueOrDefault(DicomTag.StudyInstanceUID, string.Empty),
            NumberOfSeries = dataset.GetSingleValueOrDefault(DicomTag.NumberOfStudyRelatedSeries, 0),
            NumberOfInstances = dataset.GetSingleValueOrDefault(DicomTag.NumberOfStudyRelatedInstances, 0)
        };
    }
}

// 添加Series结果转换类
public class DicomSeriesResult
{
    public string SeriesInstanceUid { get; set; } = string.Empty;
    public string StudyInstanceUid { get; set; } = string.Empty;
    public string SeriesNumber { get; set; } = string.Empty;
    public string SeriesDescription { get; set; } = string.Empty;
    public string Modality { get; set; } = string.Empty;
    public int NumberOfInstances { get; set; }

    public static DicomSeriesResult FromDataset(DicomDataset dataset)
    {
        return new DicomSeriesResult
        {
            SeriesInstanceUid = dataset.GetSingleValueOrDefault(DicomTag.SeriesInstanceUID, string.Empty),
            StudyInstanceUid = dataset.GetSingleValueOrDefault(DicomTag.StudyInstanceUID, string.Empty),
            SeriesNumber = dataset.GetSingleValueOrDefault(DicomTag.SeriesNumber, string.Empty),
            SeriesDescription = dataset.GetSingleValueOrDefault(DicomTag.SeriesDescription, string.Empty),
            Modality = dataset.GetSingleValueOrDefault(DicomTag.Modality, string.Empty),
            NumberOfInstances = dataset.GetSingleValueOrDefault(DicomTag.NumberOfSeriesRelatedInstances, 0)
        };
    }
}

// 添加Image级别的结果转换类
public class DicomImageResult
{
    public string SopInstanceUid { get; set; } = string.Empty;
    public string StudyInstanceUid { get; set; } = string.Empty;
    public string SeriesInstanceUid { get; set; } = string.Empty;
    public string InstanceNumber { get; set; } = string.Empty;
    public string[] ImageType { get; set; } = Array.Empty<string>();
    public int Rows { get; set; }
    public int Columns { get; set; }
    public int BitsAllocated { get; set; }
    public int NumberOfFrames { get; set; }

    public static DicomImageResult FromDataset(DicomDataset dataset)
    {
        return new DicomImageResult
        {
            SopInstanceUid = dataset.GetSingleValueOrDefault(DicomTag.SOPInstanceUID, string.Empty),
            StudyInstanceUid = dataset.GetSingleValueOrDefault(DicomTag.StudyInstanceUID, string.Empty),
            SeriesInstanceUid = dataset.GetSingleValueOrDefault(DicomTag.SeriesInstanceUID, string.Empty),
            InstanceNumber = dataset.GetSingleValueOrDefault(DicomTag.InstanceNumber, string.Empty),
            ImageType = dataset.GetValues<string>(DicomTag.ImageType).ToArray(),
            Rows = dataset.GetSingleValueOrDefault(DicomTag.Rows, 0),
            Columns = dataset.GetSingleValueOrDefault(DicomTag.Columns, 0),
            BitsAllocated = dataset.GetSingleValueOrDefault(DicomTag.BitsAllocated, 0),
            NumberOfFrames = dataset.GetSingleValueOrDefault(DicomTag.NumberOfFrames, 1)
        };
    }
}

// 添加Patient级别的结果转换类
public class DicomPatientResult
{
    public string PatientId { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string PatientBirthDate { get; set; } = string.Empty;
    public string PatientSex { get; set; } = string.Empty;
    public int NumberOfStudies { get; set; }

    public static DicomPatientResult FromDataset(DicomDataset dataset)
    {
        return new DicomPatientResult
        {
            PatientId = dataset.GetSingleValueOrDefault(DicomTag.PatientID, string.Empty),
            PatientName = dataset.GetSingleValueOrDefault(DicomTag.PatientName, string.Empty),
            PatientBirthDate = dataset.GetSingleValueOrDefault(DicomTag.PatientBirthDate, string.Empty),
            PatientSex = dataset.GetSingleValueOrDefault(DicomTag.PatientSex, string.Empty),
            NumberOfStudies = dataset.GetSingleValueOrDefault(DicomTag.NumberOfPatientRelatedStudies, 0)
        };
    }
}

// 在 MoveRequest 类中添加传输语法参数
public class MoveRequest
{
    public string PatientId { get; set; } = string.Empty;
    public string StudyInstanceUid { get; set; } = string.Empty;
    public string SeriesInstanceUid { get; set; } = string.Empty;
    public string SopInstanceUid { get; set; } = string.Empty;
    // 使用字符串，可以接受 UID、代码或枚举名称
    public string? TransferSyntax { get; set; }
}