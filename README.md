
# Renderizado de una entrada de video y de audio en nuestra aplicación UWP

El objetivo de este post es mostrar cómo podemos renderizar el stream de video procedente de un dispositivo como puede ser una cámara, webcam o una capturadora de video, y el stream de audio procedente de cualquier entrada de audio.

Más concretamente, este ejemplo se pensó para un escenario donde se quería realizar un mirroring de la pantalla y el audio de un teléfono móvil dentro de nuestra aplicación UWP que se ejecuta en un PC, y ambos (PC y móvil) conectados mediante una capturadora de video.

En este post presentaremos un ejemplo básico desarrollado para UWP utilizando la clase [CaptureElement](https://docs.microsoft.com/en-us/uwp/api/windows.ui.xaml.controls.captureelement) y el API [MediaCapture](https://docs.microsoft.com/en-us/uwp/api/windows.media.capture.mediacapture) para renderizar la entrada de video y la clase [AudioGraph](https://docs.microsoft.com/en-us/uwp/api/windows.media.audio.audiograph) para enrutar la entrada de audio a la salida de audio deseada. Para simplificar y no extender en exceso el ejemplo, todo el código que se muestra ha sido añadido en el _code-behind_ de la vista, práctica que no recomendamos realizar en proyecto reales.

El código de ejemplo puede ser descargado del repositorio de [GitHub](https://github.com/WindowsPlatformTeam/VideoAndAudioCapture) .

# Añadir capacidades en el AppManifest

Para que nuestra aplicación pueda acceder a un dispositivo de video y audio, debemos establecer que nuestra aplicación va a hacer uso de las capacidades de Webcam y Micrófono. Estas capacidades las establecemos en el fichero AppManifest. Los pasos que debes seguir con los siguientes.

1. En el explorador de solución del Visual Studio, busca el fichero **package.appxmanifest** y haz doble clic sobre el para abrirlo.
2. Selecciona la pestaña **Capacidades**.
3. Selecciona las opciones **Webcam** y **Micrófono**.

Con estas dos capacidades es suficiente para nuestro ejemplo, pero si quisiéramos guardar alguna imagen o video procedentes del stream de video, necesitaríamos añadir las capacidades de Librería de Imágenes y Librería de Videos.

# Los elementos de la UI

La UI que vamos a usar para el ejemplo, es una UI básica sin ningún tipo de diseño y con los elementos básico para poder cumplir con el objetivo del post. Los elementos que necesitamos son los siguientes:

* VideoInputComboBox: Es un _ComboBox_ que utilizaremos para mostrar todas las entradas de video disponibles y de las cuales el usuario podrá seleccionar la entrada que será utilizada.
* AudioInputComboBox: Es un _ComboBox_ que utilizaremos para mostrar todas las entradas de audio disponibles y de las cuales el usuario podrá seleccionar la entrada que será utilizada.
* StartButton: Es el _Button_ que utilizaremos para comenzar la visualización de la entrada de video y el enrutado de la entrada de sonido.
* StopButton: Es el _Button_ que utilizaremos para parar la acción del StartButton.
* CaptureElement: Es el control que mostrará en la UI el video procedente de la entrada de video.

A continuación, se muestra el código de la UI que será añadido en la vista deseada. En nuestro caso lo añadimos en la MainView.

```:yamldecode:true
<Grid Background="{ThemeResource ApplicationPageBackgroundThemeBrush}">
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="200"/>
        <ColumnDefinition Width="*"/>
    </Grid.ColumnDefinitions>
    <StackPanel>
        <ComboBox x:Name="VideoInputComboBox" Header="Video Input" DisplayMemberPath="Name"
                HorizontalAlignment="Stretch"
                SelectionChanged="VideoInputComboBoxSelectionChanged"/>
        <ComboBox x:Name="AudioInputComboBox" Header="Audio Input" DisplayMemberPath="Name"
                HorizontalAlignment="Stretch"
                SelectionChanged="AudioInputComboBoxSelectionChanged"
                Margin="0,20,0,0"/>
        <StackPanel Orientation="Horizontal" Margin="0,20,0,0">
            <Button x:Name="StartButton" Content="Start" IsEnabled="False" Click="StartButtonClick"/>
            <Button x:Name="StopButton" Content="Stop" IsEnabled="False" Click="StopButtonClick" Margin="20,0,0,0"/>
        </StackPanel>
    </StackPanel>
    <CaptureElement x:Name="CaptureElement" Grid.Column="1"/>
</Grid>
```

Una vez que tenemos la parte de UI preparada, vamos a continuar con la lógica de nuestro ejemplo.

# Selección de los dispositivos de entrada de video y audio

Lo primero que necesitamos es mostrar al usuario las entradas de video y audio disponibles, para que pueda seleccionar la que desea. Para ello utilizamos la clase [DeviceInformation](https://docs.microsoft.com/en-us/uwp/api/windows.devices.enumeration.deviceinformation) como se muestra en el método **LoadDevices** . Este método puede ser llamado desde el método **OnNavigatedTo** de la vista.

```:c#decode:true
private async Task LoadDevices()
{
    VideoInputComboBox.ItemsSource = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
    AudioInputComboBox.ItemsSource = await DeviceInformation.FindAllAsync(MediaDevice.GetAudioCaptureSelector());
}
```

```:c#decode:true
protected override async void OnNavigatedTo(NavigationEventArgs e)
{
    base.OnNavigatedTo(e);
    await LoadDevices();
}
```

Al método **FindAllAsync** de la clase DeviceInformation le pasamos como parámetro el valor **DeviceClass.VideoCapture** para filtrar de entre todos los dispositivos, los dispositivos de entrada de video.

```:c#decode:true
VideoInputComboBox.ItemsSource = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
```

Para obtener los dispositivos de entrada de audio, utilizamos el método FindAllAsync con un identificador que representa a los dispositivos de entrada de audio, que es obtenido con el método **MediaDevice.GetAudioCaptureSelector** .

```:c#decode:true
AudioInputComboBox.ItemsSource = await DeviceInformation.FindAllAsync(MediaDevice.GetAudioCaptureSelector());
```

Cuando el usuario selecciona un dispositivo de entrada de video, guardamos en una variable dicho dispositivo que será utilizados posteriormente para cargar el video de dicha entrada.

```:c#decode:true
private void VideoInputComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
{
    _videoInputSelected = e.AddedItems?.FirstOrDefault() as DeviceInformation;
}
```

Cuando el usuario selecciona un dispositivo de entrada de audio, guardamos en una variable dicho dispositivo que será utilizados posteriormente para enrutar el sonido desde dicha entrada.

```:c#decode:true
private void AudioInputComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
{
    _audioInputSelected = e.AddedItems?.FirstOrDefault() as DeviceInformation;
}
```

# Lanzar previsualización del stream de video

Una vez que ya podemos seleccionar las entradas de video y audio, solamente nos falta lanzar la previsualización del stream de video y el enrutado de la entrada de sonido hacia una salida de sonido predefinida.

Para trabajar con la entrada de video vamos a utilizar la clase [MediaCapture](https://docs.microsoft.com/en-us/uwp/api/windows.media.capture.mediacapture) . Creamos una instancia de MediaCapture y la inicializamos con el identificador del dispositivo de video seleccionado. Tras inicializar el MediaCapture lo asignamos como Source del elemento **CaptureElement** creado en la UI. De este modo, el stream de video cargado mediante el MediaCapture será mostrado en el CaptureElement. Posteriormente, lanzamos la previsualización con el método **StartPreviewAsync** . A continuación, se muestra el código correspondiente en el método StartVideoSsync.

```:c#decode:true
private async Task StartVideoAsync()
{
    try
    {
        _mediaCapture = new MediaCapture();

        if (_mediaCapture != null)
        {
            await _mediaCapture.InitializeAsync(new MediaCaptureInitializationSettings() { VideoDeviceId = _videoInputSelected.Id });
            CaptureElement.Source = _mediaCapture;
            await _mediaCapture.StartPreviewAsync();

            _isPreviewing = true;
            _displayRequest = new DisplayRequest();
            _displayRequest.RequestActive();
            _displayRequested = true;
        }
    }
    catch (UnauthorizedAccessException)
    {
        // This will be thrown if the user denied access to the camera in privacy settings
        Debug.WriteLine("The app was denied access to the camera");
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"MediaCapture initialization failed. {ex?.Message}");
    }
}
```

Si queremos que el sistema que ejecuta nuestra aplicación no entre en estado de ahorro de energía mientras se ejecuta la aplicación, hacemos uso de la clase [DisplayRequest](https://docs.microsoft.com/en-us/uwp/api/windows.system.display.displayrequest) . Con el método **RequestActive** realizamos una solicitud de presentación, para notificar al sistema de que no entre en estado de ahorro de energía. Cuando se activa una solicitud de presentación, la presentación de dispositivo permanece activa mientras la aplicación está visible.

Por otro lado, destacar que si queremos detectar el caso en el que no tenemos acceso a la entrada de video seleccionada, debemos capturar la excepción **UnauthorizedAccessException** .

# Lanzar enrutado de la entrada de audio seleccionada a la salida de audio por defecto

Llegados a este punto, ya tenemos la previsualización de la entrada de video en nuestra aplicación. Ahora necesitamos que el sonido que recibimos por la entrada de sonido que ha seleccionado el usuario se reproduzca por la salida de audio predeterminada.

En el caso del audio vamos a utilizar la clase [AudioGraph](https://docs.microsoft.com/en-us/uwp/api/windows.media.audio.audiograph), que nos permite de manera sencilla trabajar con los nodos de audio. La clase AudioGraph nos permite trabajar con el grafo de audio, que viene a ser un conjunto de nodos de audio interconectados, a través de los cuales fluyen los datos de audio. Existen nodos de entrada, de salida y nodos de submezcla, que pillan varios nodos y crean una salida de audio. Ver más información [aquí](https://docs.microsoft.com/en-us/uwp/api/windows.media.audio.audiograph) .

Nosotros simplemente necesitamos crear un flujo para que los datos de un nodo de entrada (dispositivo de entrada de audio) fluyan hacia un nodo de salida (dispositivo de salida de audio predeterminado).

Lo primero que necesitamos hacer es crear un AudioGraph mediante el método **CreateAsync** . Si el resultado es correcto, tendremos una instancia de AudioGraph. A continuación, haciendo uso de la instancia de AudioGraph, necesitamos crear un nodo de entrada a partir del dispositivo de entrada de audio seleccionado por el usuario, y un nodo de salida correspondiente al dispositivo de salida de audio por defecto. Para ello hacemos uso del método **CreateDeviceInputNodeAsync** y del método **CreateDeviceOutputNodeAsync** respectivamente.

Una vez que tenemos los dos nodos creados, enlazamos el nodo de salida con el nodo de entrada mediante el método AddOutgoingConnection. De este modo ya tenemos el flujo enrutado y solamente nos faltaría iniciar el flujo llamando al método **Start** de la instancia de AudioGraph.

```:c#decode:true
private async Task StartAudioAsync()
{
    AudioGraphSettings settings = new AudioGraphSettings(AudioRenderCategory.Media);
    settings.QuantumSizeSelectionMode = QuantumSizeSelectionMode.LowestLatency;

    try
    {
        CreateAudioGraphResult result = await AudioGraph.CreateAsync(settings);
        if (result.Status != AudioGraphCreationStatus.Success) return;

        _audioGraph = result.Graph;

        // Create a device input node
        CreateAudioDeviceInputNodeResult deviceInputNodeResult = await _audioGraph.CreateDeviceInputNodeAsync(MediaCategory.Media, _audioGraph.EncodingProperties, _audioInputSelected);
        if (deviceInputNodeResult.Status != AudioDeviceNodeCreationStatus.Success) return;

        // Create a device output node
        CreateAudioDeviceOutputNodeResult deviceOutputNodeResult = await _audioGraph.CreateDeviceOutputNodeAsync();
        if (deviceOutputNodeResult.Status != AudioDeviceNodeCreationStatus.Success) return;

        _deviceInputNode = deviceInputNodeResult.DeviceInputNode;
        _deviceOutputNode = deviceOutputNodeResult.DeviceOutputNode;

        _deviceInputNode.AddOutgoingConnection(_deviceOutputNode);
        _audioGraph.Start();
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"AudioGraph initialization failed. {ex?.Message}");
    }
}
```

El manejador del evento _click_ del botón StartButton se encargará de llamar al método StartVideoAsync y StartAudioAsync para lanzar la captura del video y el audio.

```:c#decode:true
private async void StartButtonClick(object sender, Windows.UI.Xaml.RoutedEventArgs e)
{
    await StartVideoAsync();
    await StartAudioAsync();
}
```

# Liberar los recursos de audio y video

Una vez que ya tenemos todo preparado para reproducir el video y el audio, necesitamos en algún momento poder parar y liberar los recursos. Lo primero que debemos hacer es para la previsualización del video, para ello debemos hacer un **StopPreviewAsync** del MediaCapture y anular la propiedad Source del CaptureElement que está mostrando el video en la UI.

En el caso de haber realizado una solicitud de presentación con _displayRequest.RequestActive() en el apartado de "Lanzar previsualización del stream de video", ahora debemos liberar la solicitud con el método **RequestRelease** de la instancia de DisplayRequest.

Finalmente, hacemos un Dispose de la instancia de MediaCapture para liberar memoria.

```:c#decode:true
private async Task StopVideoAsync()
{
    if (_mediaCapture != null && _isPreviewing)
    {
        try
        {
            _isPreviewing = false;
            await _mediaCapture.StopPreviewAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Exception when stopping the preview: {0}", ex.ToString());
        }

        // Use the dispatcher because this method is sometimes called from non-UI threads
        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
        {
            // Cleanup the UI
            CaptureElement.Source = null;

            // Allow the device screen to sleep now that the preview is stopped
            if (_displayRequest != null && _displayRequested)
            {
                _displayRequest.RequestRelease();
            }
        });

        _mediaCapture.Dispose();
        _mediaCapture = null;
    }
}
```

Para finalizar con el grafo de audio simplemente debemos llamar al método Stop de la instancia de AudioGraph, hacer un Dispose de ambos nodos (entra y salida de audio) y finalmente, hacer un Dispose de la instancia de AudioGraph. De este modo, liberaremos los recursos y la memoria de los mismos.

```:c#decode:true
private void StopAudio()
{
    if (_audioGraph != null)
    {
        try
        {
            _audioGraph.Stop();

            _deviceInputNode?.Dispose();
            _deviceOutputNode?.Dispose();
            _audioGraph.Dispose();

            _audioGraph = null;
            _deviceInputNode = null;
            _deviceOutputNode = null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Exception when stopping the AudioGraph: {0}", ex.ToString());
        }
    }
}
```

Haremos uso del manejador del evento click del botón StopButton para llamar al método StopVideoAsync y StopAudio y parar así la captura del video y el audio.

```:c#decode:true
private async void StopButtonClick(object sender, Windows.UI.Xaml.RoutedEventArgs e)
{
    await StopVideoAsync();
    StopAudio();
}
```

# Referencias

A continuación dejamos algunos enlaces que pueden resultar de interés.

* Repositorio del ejemplo:[https://github.com/WindowsPlatformTeam/VideoAndAudioCapture](https://github.com/WindowsPlatformTeam/VideoAndAudioCapture)
* Clase [MediaCapture](https://docs.microsoft.com/en-us/uwp/api/windows.media.capture.mediacapture)
* Clase [AudioGraph](https://docs.microsoft.com/en-us/uwp/api/windows.media.audio.audiograph)
* Clase [DisplayRequest](https://docs.microsoft.com/en-us/uwp/api/windows.system.display.displayrequest)
