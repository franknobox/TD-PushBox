using System;
using System.IO;
using System.Text;

namespace JobTest.TacticsVoxel
{
    public static class PushBoxSoundSynthesis
    {
        private const int Rate=44100;
        private sealed class Noise
        {
            private uint state;
            public Noise(uint seed)=>state=seed;
            public double Next()
            {state^=state<<13;state^=state>>17;state^=state<<5;return (state>>8)/8388608.0-1;}
        }

        public static void WriteAll(string folder)
        {
            Directory.CreateDirectory(folder);
            for(int i=0;i<4;i++) Write(Path.Combine(folder,$"Footstep_{i+1:00}.wav"),Footstep(i),0.72);
            for(int i=0;i<2;i++) Write(Path.Combine(folder,$"CrateDrag_{i+1:00}.wav"),Drag(i),0.66);
            Write(Path.Combine(folder,"MagicSwap.wav"),Magic(),0.84);
        }
        private static double[] Footstep(int variation)
        {
            double duration=0.12+variation*0.006;var samples=new double[(int)(Rate*duration)];
            var noise=new Noise((uint)(8423+variation*173));double low=0,body=0;
            for(int i=0;i<samples.Length;i++)
            {
                double t=i/(double)Rate,n=noise.Next();low+=0.23*(n-low);body+=0.035*(n-body);
                double attack=1-Math.Exp(-t/0.0018),impact=Math.Exp(-t/0.028),grit=Math.Exp(-t/0.047);
                double frequency=118+variation*9;
                samples[i]=attack*(0.43*Math.Sin(2*Math.PI*(frequency*t-130*t*t))*impact
                    +0.95*(low-body)*grit+0.10*(n-low)*Math.Exp(-t/0.012));
                samples[i]*=Tail(t,duration);
            }
            return samples;
        }
        private static double[] Drag(int variation)
        {
            double duration=0.195+variation*0.018;var samples=new double[(int)(Rate*duration)];
            var noise=new Noise((uint)(52531+variation*337));double low=0,slow=0,phase=0;
            for(int i=0;i<samples.Length;i++)
            {
                double t=i/(double)Rate,n=noise.Next();low+=0.19*(n-low);slow+=0.018*(n-slow);
                double grain=0.65+0.20*Math.Sin(2*Math.PI*39*t)+0.15*Math.Sin(2*Math.PI*67*t+variation);
                double envelope=(1-Math.Exp(-t/0.006))*Math.Pow(Math.Max(0,1-t/duration),0.65);
                phase+=2*Math.PI*(205+variation*22+15*Math.Sin(2*Math.PI*21*t))/Rate;
                double wood=Math.Sin(phase)*0.055+Math.Sin(phase*2.71)*0.028;
                double settle=t>0.12?0.12*Math.Sin(2*Math.PI*143*(t-0.12))*Math.Exp(-(t-0.12)/0.018):0;
                samples[i]=((low-slow)*grain*1.5+wood)*envelope+settle;
                samples[i]*=Tail(t,duration);
            }
            return samples;
        }
        private static double[] Magic()
        {
            const double duration=0.32;var samples=new double[(int)(Rate*duration)];
            var noise=new Noise(583103);double low=0,air=0,phase=0;
            for(int i=0;i<samples.Length;i++)
            {
                double t=i/(double)Rate,n=noise.Next();low+=0.24*(n-low);air+=0.035*(n-air);
                phase+=2*Math.PI*(92+230*Math.Exp(-t/0.024))/Rate;
                double attack=1-Math.Exp(-t/0.0015);
                double pop=(Math.Sin(phase)*0.60+Math.Sin(phase*0.49)*0.20)*Math.Exp(-t/0.064);
                double puff=(low-air)*1.4*Math.Exp(-t/0.043);
                double sparkle=0.045*Math.Sin(2*Math.PI*(1120*t+700*t*t))*Math.Exp(-t/0.085);
                samples[i]=attack*(pop+puff+sparkle)*Tail(t,duration);
            }
            return samples;
        }
        private static double Tail(double time,double duration)=>Math.Max(0,Math.Min(1,(duration-time)/0.012));
        private static void Write(string path,double[] samples,double peak)
        {
            if(File.Exists(path)) return;
            double maximum=0;foreach(double sample in samples) maximum=Math.Max(maximum,Math.Abs(sample));
            double gain=maximum>0?peak/maximum:1;samples[0]=samples[samples.Length-1]=0;
            using(var writer=new BinaryWriter(File.Create(path)))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+samples.Length*2);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(Rate);writer.Write(Rate*2);writer.Write((short)2);writer.Write((short)16);
                writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(samples.Length*2);
                foreach(double sample in samples) writer.Write((short)Math.Round(Math.Max(-0.95,Math.Min(0.95,sample*gain))*32767));
            }
        }
    }
}
