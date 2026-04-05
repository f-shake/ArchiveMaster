using ArchiveMaster.Configs;
using ArchiveMaster.Services;
using ArchiveMaster.ViewModels.FileSystem;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FzLib.Avalonia.Dialogs;

namespace ArchiveMaster.ViewModels;

public partial class PhotoTagGeneratorViewModel(ViewModelServices services)
    : TwoStepViewModelBase<PhotoTagGeneratorService,PhotoTagGeneratorConfig>(services)
{
    [RelayCommand]
    private async Task TestAsync()
    {
        LlmCallerService llm = new LlmCallerService(GlobalConfigs.Instance.AiProviders.CurrentProvider);
        var s =await llm.CallWithStreamAsync(new List<AiChatMessage>
        {
            AiChatMessage.CreateSystemMessage("""
                                              1.角色：图像语义原子化标注员
                                              2.任务：请对用户提供的图片进行内容提炼，输出 5-20 个核心关键词。
                                              3.原子化拆解：每个词语必须是不可再分的最小语义单位。严禁合成词，例如：禁止“夕阳西下”，应拆分为“夕阳，日落”；禁止“城市建筑”，应拆分为“城市，建筑”。
                                              4.字数约束：每个关键词限 2-3 字。
                                              5.输出格式：仅输出结果，禁止任何开场白、解释或总结。词语间固定使用中文逗号（，）分隔。
                                              6.维度要求：关键词应涵盖【核心主体、场景环境、氛围情感、色彩基调】。
                                              7.例如：大桥，斜拉桥，高楼，河流，水面，城市，蓝天，建筑，现代
                                              8.不应该输出例如：水库风光，梯田景观，乡村道路，绿色植被，航拍视角，田园风光，山水相依，农业种植，蜿蜒公路，生态和谐
                                              """),
            AiChatMessage.CreateUserMessage("",
                [await File.ReadAllBytesAsync(@"C:\Users\autod\Desktop\D_DJI_20260124143450_0262_D(Lr).jpg")])
        });
        Console.WriteLine(s);
    }
}